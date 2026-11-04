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

/** Result of combining signals: dispose removes the listeners the combination installed */
export interface CombinedSignal {
    signal: AbortSignal;
    dispose: () => void;
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
            const { signal: abortSignal, dispose } = this.combined(signal);
            try {
                for await (const tenant of client.tenants.list({ abortSignal })) {
                    if (tenant.tenantId) {
                        tenants.push({ tenantId: tenant.tenantId, displayName: tenant.displayName });
                    }
                }
            } finally {
                dispose();
            }
            return tenants;
        });
    }

    public async getSubscriptionsAsync(signal?: AbortSignal): Promise<SubscriptionInfo[]> {
        const client = new SubscriptionClient(getCredential());

        return this.trackLoad(async () => {
            const subscriptions: SubscriptionInfo[] = [];
            const { signal: abortSignal, dispose } = this.combined(signal);
            try {
                for await (const subscription of client.subscriptions.list({ abortSignal })) {
                    if (subscription.subscriptionId) {
                        subscriptions.push({
                            subscriptionId: subscription.subscriptionId,
                            displayName: subscription.displayName ?? subscription.subscriptionId,
                            tenantId: subscription.tenantId ?? "",
                        });
                    }
                }
            } finally {
                dispose();
            }
            return subscriptions;
        });
    }

    public async getResourceGroupsAsync(subscription: SubscriptionInfo, signal?: AbortSignal): Promise<ResourceGroupInfo[]> {
        const client = this.resourceClient(subscription);

        return this.trackLoad(async () => {
            const groups: ResourceGroupInfo[] = [];
            const { signal: abortSignal, dispose } = this.combined(signal);
            try {
                for await (const group of client.resourceGroups.list({ abortSignal })) {
                    if (group.name) {
                        groups.push({ name: group.name, id: group.id ?? "" });
                    }
                }
            } finally {
                dispose();
            }
            return groups;
        });
    }

    public async getLogicAppsAsync(subscription: SubscriptionInfo, resourceGroupName: string, signal?: AbortSignal): Promise<AppResourceInfo[]> {
        const client = this.resourceClient(subscription);

        return this.trackLoad(async () => {
            const { signal: abortSignal, dispose } = this.combined(signal);
            try {
                const resources = this.listByType(client, resourceGroupName, "Microsoft.Logic/workflows", abortSignal);
                const apps: AppResourceInfo[] = [];

                for await (const resource of resources) {
                    apps.push(await this.toAppResource(subscription, resource, resource.name ?? "unknown", abortSignal));
                }

                return apps;
            } finally {
                dispose();
            }
        });
    }

    public async getWebAppsAsync(subscription: SubscriptionInfo, resourceGroupName: string, signal?: AbortSignal): Promise<Array<AppResourceInfo & { kind: WebAppKind }>> {
        const client = this.resourceClient(subscription);

        return this.trackLoad(async () => {
            const { signal: abortSignal, dispose } = this.combined(signal);
            try {
                const resources = this.listByType(client, resourceGroupName, "Microsoft.Web/sites", abortSignal);
                const apps: Array<AppResourceInfo & { kind: WebAppKind }> = [];

                for await (const resource of resources) {
                    // the kind can contain several comma-separated values, e.g. "functionapp,linux"
                    const kind = (resource.kind ?? "app").includes("functionapp") ? "function" : "web";
                    apps.push({
                        kind,
                        ...(await this.toAppResource(subscription, resource, resource.name ?? "unknown", abortSignal)),
                    });
                }

                return apps;
            } finally {
                dispose();
            }
        });
    }

    private listByType(client: ResourceManagementClient, resourceGroupName: string, resourceType: string, abortSignal: AbortSignal) {
        return client.resources.listByResourceGroup(resourceGroupName, {
            filter: `resourceType eq '${resourceType}'`,
            abortSignal,
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

            const { signal: abortSignal, dispose } = this.combined(signal);
            try {
                const appSettings = await client.webApps.listApplicationSettings(groupName, appName, {
                    abortSignal,
                });

                return appSettings.properties ? { ...appSettings.properties } : null;
            } finally {
                dispose();
            }
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

    /**
     * Combines the session-wide signal with a per-call one, like CombineTokens of the C# original.
     * The caller must call dispose when the operation settles, otherwise the per-call listener
     * stays attached to the long-lived session signal and accumulates until a cancel.
     */
    private combined(signal?: AbortSignal): CombinedSignal {
        const live = [this.loadingSession.signal, signal].filter((s): s is AbortSignal => !!s);
        if (live.length === 1) {
            // only the session signal is used - no listener is installed, nothing to dispose
            return { signal: live[0], dispose: () => {} };
        }

        const controller = new AbortController();
        const installed: Array<{ source: AbortSignal; abort: () => void }> = [];
        const dispose = () => {
            for (const entry of installed.splice(0)) {
                entry.source.removeEventListener("abort", entry.abort);
            }
        };
        const abort = () => {
            dispose();
            controller.abort();
        };

        for (const source of live) {
            if (source.aborted) {
                controller.abort();
                break;
            }
            installed.push({ source, abort });
            source.addEventListener("abort", abort);
        }

        return { signal: controller.signal, dispose };
    }
}
