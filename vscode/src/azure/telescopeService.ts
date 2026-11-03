import { ResourceManagementClient, type GenericResource } from "@azure/arm-resources";
import { SubscriptionClient } from "@azure/arm-resources-subscriptions";
import { WebSiteManagementClient } from "@azure/arm-appservice";
import { getCredential } from "./auth";

/**
 * Port of src/Core/Services/TelescopeService.cs.
 * Queries tenants, subscriptions, resource groups and the app resources
 * (Logic Apps, App Services, Function Apps) with their configuration.
 */
export interface TenantInfo {
    tenantId: string;
    displayName?: string;
}

export interface SubscriptionInfo {
    subscriptionId: string;
    displayName: string;
    tenantId: string;
}

export interface ResourceGroupInfo {
    name: string;
    id: string;
}

export interface AppResourceInfo {
    /** Resource name */
    name: string;
    /** Full ARM id */
    armId: string;
    /** Resource configuration (properties) serialized as indented JSON, or null */
    data: string | null;
    tags: Record<string, string> | null;
    /** App settings, or null when they cannot be retrieved */
    environmentVariables: Record<string, string> | null;
}

export type WebAppKind = "function" | "web";

/** Combines the session-wide signal with a per-call one, like CombineTokens of the C# original */
function combineSignals(...signals: Array<AbortSignal | undefined>): AbortSignal {
    const live = signals.filter((signal): signal is AbortSignal => !!signal);
    if (live.length === 1) {
        return live[0];
    }

    const controller = new AbortController();
    const abort = () => controller.abort();
    for (const signal of live) {
        if (signal.aborted) {
            controller.abort();
            break;
        }
        signal.addEventListener("abort", abort, { once: true });
    }
    return controller.signal;
}

export class TelescopeService {
    // cancels all the loading operations of the current loading session
    private loadingSession = new AbortController();

    private loadingCount = 0;
    private readonly loadingListeners = new Set<(isLoading: boolean) => void>();

    private readonly resourceClients = new Map<string, ResourceManagementClient>();
    private readonly websiteClients = new Map<string, WebSiteManagementClient>();

    /** Cancels all the loading operations that are currently in progress */
    public cancelLoading(): void {
        this.loadingSession.abort();
        this.loadingSession = new AbortController();
    }

    /** Reports whether any load is in flight; used to drive the tree busy state */
    public onLoadingChange(listener: (isLoading: boolean) => void): { dispose: () => void } {
        this.loadingListeners.add(listener);
        return { dispose: () => this.loadingListeners.delete(listener) };
    }

    private async trackLoad<T>(action: () => Promise<T>): Promise<T> {
        this.loadingCount++;
        if (this.loadingCount === 1) {
            this.notifyLoading();
        }

        try {
            return await action();
        } finally {
            this.loadingCount--;
            if (this.loadingCount === 0) {
                this.notifyLoading();
            }
        }
    }

    private notifyLoading(): void {
        const isLoading = this.loadingCount > 0;
        for (const listener of this.loadingListeners) {
            listener(isLoading);
        }
    }

    public async getTenantsAsync(signal?: AbortSignal): Promise<TenantInfo[]> {
        const client = new SubscriptionClient(getCredential());

        return this.trackLoad(async () => {
            const tenants: TenantInfo[] = [];
            for await (const tenant of client.tenants.list({ abortSignal: this.combined(signal) })) {
                if (tenant.tenantId) {
                    tenants.push({ tenantId: tenant.tenantId, displayName: tenant.displayName });
                }
            }
            return tenants;
        });
    }

    public async getSubscriptionsAsync(signal?: AbortSignal): Promise<SubscriptionInfo[]> {
        const client = new SubscriptionClient(getCredential());

        return this.trackLoad(async () => {
            const subscriptions: SubscriptionInfo[] = [];
            for await (const subscription of client.subscriptions.list({ abortSignal: this.combined(signal) })) {
                if (subscription.subscriptionId) {
                    subscriptions.push({
                        subscriptionId: subscription.subscriptionId,
                        displayName: subscription.displayName ?? subscription.subscriptionId,
                        tenantId: subscription.tenantId ?? "",
                    });
                }
            }
            return subscriptions;
        });
    }

    public async getResourceGroupsAsync(subscription: SubscriptionInfo, signal?: AbortSignal): Promise<ResourceGroupInfo[]> {
        const client = this.resourceClient(subscription);

        return this.trackLoad(async () => {
            const groups: ResourceGroupInfo[] = [];
            for await (const group of client.resourceGroups.list({ abortSignal: this.combined(signal) })) {
                if (group.name) {
                    groups.push({ name: group.name, id: group.id ?? "" });
                }
            }
            return groups;
        });
    }

    public async getLogicAppsAsync(subscription: SubscriptionInfo, resourceGroupName: string, signal?: AbortSignal): Promise<AppResourceInfo[]> {
        const client = this.resourceClient(subscription);

        return this.trackLoad(async () => {
            const resources = this.listByType(client, resourceGroupName, "Microsoft.Logic/workflows", signal);
            const apps: AppResourceInfo[] = [];

            for await (const resource of resources) {
                apps.push(await this.toAppResource(subscription, resource, resource.name ?? "unknown", signal));
            }

            return apps;
        });
    }

    public async getWebAppsAsync(subscription: SubscriptionInfo, resourceGroupName: string, signal?: AbortSignal): Promise<Array<AppResourceInfo & { kind: WebAppKind }>> {
        const client = this.resourceClient(subscription);

        return this.trackLoad(async () => {
            const resources = this.listByType(client, resourceGroupName, "Microsoft.Web/sites", signal);
            const apps: Array<AppResourceInfo & { kind: WebAppKind }> = [];

            for await (const resource of resources) {
                // the kind can contain several comma-separated values, e.g. "functionapp,linux"
                const kind = (resource.kind ?? "app").includes("functionapp") ? "function" : "web";
                apps.push({
                    kind,
                    ...(await this.toAppResource(subscription, resource, resource.name ?? "unknown", signal)),
                });
            }

            return apps;
        });
    }

    private listByType(client: ResourceManagementClient, resourceGroupName: string, resourceType: string, signal?: AbortSignal) {
        return client.resources.listByResourceGroup(resourceGroupName, {
            filter: `resourceType eq '${resourceType}'`,
            abortSignal: this.combined(signal),
        });
    }

    private async toAppResource(
        subscription: SubscriptionInfo,
        resource: GenericResource,
        name: string,
        signal?: AbortSignal,
    ): Promise<AppResourceInfo> {
        return {
            name,
            armId: resource.id ?? "",
            data: resource.properties !== undefined && resource.properties !== null
                ? JSON.stringify(resource.properties, null, 4) // match Newtonsoft.Formatting.Indented of the C# original
                : null,
            tags: resource.tags ? { ...resource.tags } : null,
            environmentVariables: await this.getEnvironmentVariablesAsync(subscription, name, resource, signal),
        };
    }

    /**
     * Returns the environment variables (app settings) of a web/function app,
     * or null if they cannot be retrieved
     */
    private async getEnvironmentVariablesAsync(
        subscription: SubscriptionInfo,
        appName: string,
        resource: GenericResource,
        signal?: AbortSignal,
    ): Promise<Record<string, string> | null> {
        try {
            const client = this.websiteClient(subscription);
            const groupName = this.resourceGroupNameOf(resource);
            if (!groupName) {
                return null;
            }

            const appSettings = await client.webApps.listApplicationSettings(groupName, appName, {
                abortSignal: this.combined(signal),
            });

            return appSettings.properties ? { ...appSettings.properties } : null;
        } catch {
            // the environment variables are not critical for the app representation, so just skip them in case of any issue
            return null;
        }
    }

    private resourceGroupNameOf(resource: GenericResource): string | undefined {
        // /subscriptions/<sub>/resourceGroups/<rg>/providers/...
        const match = resource.id?.match(/resourceGroups\/([^/]+)/i);
        return match?.[1];
    }

    private resourceClient(subscription: SubscriptionInfo): ResourceManagementClient {
        let client = this.resourceClients.get(subscription.subscriptionId);
        if (!client) {
            client = new ResourceManagementClient(getCredential(subscription.tenantId || undefined), subscription.subscriptionId);
            this.resourceClients.set(subscription.subscriptionId, client);
        }
        return client;
    }

    private websiteClient(subscription: SubscriptionInfo): WebSiteManagementClient {
        let client = this.websiteClients.get(subscription.subscriptionId);
        if (!client) {
            client = new WebSiteManagementClient(getCredential(subscription.tenantId || undefined), subscription.subscriptionId);
            this.websiteClients.set(subscription.subscriptionId, client);
        }
        return client;
    }

    private combined(signal?: AbortSignal): AbortSignal {
        return combineSignals(this.loadingSession.signal, signal);
    }
}
