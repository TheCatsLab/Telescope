import * as vscode from "vscode";

import { TelescopeService, type AppResourceInfo, type SubscriptionInfo, type TenantInfo } from "../azure/telescopeService";
import { isAuthenticated } from "../azure/auth";
import { ResourceNode, ResourceNodeType } from "../models/resourceNode";

/**
 * The Telescope resource tree: tenants, subscriptions, resource groups
 * and the app resources. Port of the MainWindow tree with the lazy loading
 * contract of the WPF original (expand triggers a load, errors offer a retry).
 */
export class TelescopeTreeProvider implements vscode.TreeDataProvider<ResourceNode> {
    private readonly _onDidChangeTreeData = new vscode.EventEmitter<ResourceNode | ResourceNode[] | undefined | null>();
    public readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

    private roots: ResourceNode[] | undefined;

    constructor(
        private readonly service: TelescopeService,
        private readonly extensionUri: vscode.Uri,
    ) {}

    public getTreeItem(element: ResourceNode): vscode.TreeItem {
        const item = new vscode.TreeItem(element.label, this.collapsibleStateOf(element));

        item.iconPath = this.iconOf(element);
        item.tooltip = this.tooltipOf(element);
        item.contextValue = this.contextValueOf(element);

        if (element.type === ResourceNodeType.Tenant) {
            item.description = element.id;
        } else if (element.type === ResourceNodeType.Subscription) {
            item.description = element.id;
        }

        if (this.isResourceLeaf(element)) {
            item.command = {
                command: "telescope.openDetails",
                title: "Open Details",
                arguments: [element],
            };
        }

        if (element.type === ResourceNodeType.SignIn) {
            item.command = {
                command: "telescope.signIn",
                title: "Sign In to Azure",
                arguments: [],
            };
        }

        if (element.type === ResourceNodeType.Error) {
            item.command = {
                command: "telescope.retryLoading",
                title: "Retry Loading",
                arguments: [element.parent],
            };
        }

        return item;
    }

    public async getChildren(element?: ResourceNode): Promise<ResourceNode[]> {
        try {
            if (!element) {
                return this.getRoots();
            }

            if (element.children !== undefined) {
                return element.children;
            }

            switch (element.type) {
                case ResourceNodeType.Tenant:
                    element.children = await this.loadSubscriptions(element);
                    break;
                case ResourceNodeType.Subscription:
                    element.children = await this.loadResourceGroups(element);
                    break;
                case ResourceNodeType.ResourceGroup:
                    element.children = await this.loadApps(element);
                    break;
                default:
                    // resource leaves have no children in the MVP (slots come next)
                    element.children = [];
                    break;
            }

            element.loadFailed = false;
            return element.children;
        } catch (error) {
            if (error instanceof Error && error.name === "AbortError") {
                // the loading has been cancelled by the user
                if (element) {
                    element.children = [];
                }
                return element?.children ?? [];
            }

            // replace the loading placeholder with a retryable error node
            console.error("Telescope: loading failed", error);

            if (element) {
                element.children = [this.errorNode(element)];
                element.loadFailed = true;
                return element.children;
            }

            return [this.errorNode()];
        }
    }

    /** Reloads the whole tree or a single node's children */
    public refresh(node?: ResourceNode): void {
        if (!node) {
            this.roots = undefined;
        } else {
            node.children = undefined;
            node.loadFailed = false;
        }

        this._onDidChangeTreeData.fire(node);
    }

    /**
     * Loads the ancestors of the node so it can be revealed;
     * the search controller materializes its results through this path
     */
    public async ensureAncestorsLoaded(node: ResourceNode): Promise<void> {
        const ancestors = node.ancestors().slice(0, -1); // everything except the node itself
        for (const ancestor of ancestors) {
            if (ancestor.children === undefined) {
                await this.getChildren(ancestor);
            }
        }
    }

    private async getRoots(): Promise<ResourceNode[]> {
        if (this.roots !== undefined) {
            return this.roots;
        }

        if (!(await isAuthenticated())) {
            return [new ResourceNode("Sign in to Azure...", ResourceNodeType.SignIn)];
        }

        const tenants: TenantInfo[] = await this.service.getTenantsAsync();

        this.roots = tenants.map((tenant) => {
            return new ResourceNode(tenant.tenantId, ResourceNodeType.Tenant, tenant.displayName ?? tenant.tenantId);
        });

        if (this.roots.length === 0) {
            this.roots = [new ResourceNode("No tenants available for this account", ResourceNodeType.Empty)];
        }

        return this.roots;
    }

    private async loadSubscriptions(tenant: ResourceNode): Promise<ResourceNode[]> {
        const subscriptions = await this.service.getSubscriptionsAsync();

        return subscriptions
            .filter((subscription) => subscription.tenantId === tenant.id)
            .map((subscription) => this.subscriptionNode(subscription, tenant));
    }

    private subscriptionNode(subscription: SubscriptionInfo, tenant: ResourceNode): ResourceNode {
        const node = new ResourceNode(subscription.subscriptionId, ResourceNodeType.Subscription, subscription.displayName);
        node.armId = `/subscriptions/${subscription.subscriptionId}`;
        node.tenantId = tenant.id;
        node.parent = tenant;
        return node;
    }

    private async loadResourceGroups(subscription: ResourceNode): Promise<ResourceNode[]> {
        const subscriptionInfo: SubscriptionInfo = {
            subscriptionId: subscription.id,
            displayName: subscription.label,
            tenantId: subscription.tenantId ?? "",
        };

        const groups = await this.service.getResourceGroupsAsync(subscriptionInfo);

        return groups.map((group) => {
            const node = new ResourceNode(group.name, ResourceNodeType.ResourceGroup);
            node.armId = group.id;
            node.tenantId = subscription.tenantId;
            node.parent = subscription;
            return node;
        });
    }

    private async loadApps(group: ResourceNode): Promise<ResourceNode[]> {
        const subscription = group.parent!;
        const subscriptionInfo: SubscriptionInfo = {
            subscriptionId: subscription.id,
            displayName: subscription.label,
            tenantId: subscription.tenantId ?? "",
        };

        const [logicApps, webApps] = await Promise.all([
            this.service.getLogicAppsAsync(subscriptionInfo, group.id),
            this.service.getWebAppsAsync(subscriptionInfo, group.id),
        ]);

        const nodes = [
            ...logicApps.map((app) => this.appNode(app, ResourceNodeType.LogicApp, group)),
            ...webApps.map((app) => this.appNode(app, app.kind === "function" ? ResourceNodeType.Function : ResourceNodeType.WebService, group)),
        ];

        nodes.sort((left, right) => left.label.localeCompare(right.label));

        if (nodes.length === 0) {
            return [new ResourceNode("No Logic Apps, App Services or Function Apps", ResourceNodeType.Empty, "No Logic Apps, App Services or Function Apps")];
        }

        return nodes;
    }

    private appNode(app: AppResourceInfo, type: ResourceNodeType, group: ResourceNode): ResourceNode {
        const node = new ResourceNode(app.name, type);
        node.armId = app.armId;
        node.data = app.data;
        node.tags = app.tags;
        node.environmentVariables = app.environmentVariables;
        node.tenantId = group.tenantId;
        node.parent = group;
        return node;
    }

    private errorNode(parent?: ResourceNode): ResourceNode {
        const node = new ResourceNode("Loading failed. Click to retry", ResourceNodeType.Error);
        node.parent = parent;
        return node;
    }

    private isResourceLeaf(element: ResourceNode): boolean {
        return element.type === ResourceNodeType.LogicApp
            || element.type === ResourceNodeType.Function
            || element.type === ResourceNodeType.WebService;
    }

    private collapsibleStateOf(element: ResourceNode): vscode.TreeItemCollapsibleState {
        switch (element.type) {
            case ResourceNodeType.Tenant:
            case ResourceNodeType.Subscription:
            case ResourceNodeType.ResourceGroup:
                return vscode.TreeItemCollapsibleState.Collapsed;
            default:
                return vscode.TreeItemCollapsibleState.None;
        }
    }

    private iconOf(element: ResourceNode): vscode.ThemeIcon | vscode.Uri {
        const icon = (name: string) => vscode.Uri.joinPath(this.extensionUri, "resources", "icons", name);

        switch (element.type) {
            case ResourceNodeType.Subscription:
                return icon("User-Subscriptions.svg");
            case ResourceNodeType.ResourceGroup:
                return icon("Resource-Groups.svg");
            case ResourceNodeType.LogicApp:
                return icon("Logic-Apps.svg");
            case ResourceNodeType.Function:
                return icon("Function-Apps.svg");
            case ResourceNodeType.WebService:
                return icon("App-Services.svg");
            case ResourceNodeType.SignIn:
                return new vscode.ThemeIcon("sign-in");
            case ResourceNodeType.Error:
                return new vscode.ThemeIcon("alert");
            case ResourceNodeType.Empty:
                return new vscode.ThemeIcon("circle-outline");
            default:
                return new vscode.ThemeIcon("globe");
        }
    }

    private tooltipOf(element: ResourceNode): vscode.MarkdownString | string {
        if (element.armId) {
            return element.armId;
        }

        return element.label;
    }

    private contextValueOf(element: ResourceNode): string | undefined {
        if (this.isResourceLeaf(element)) {
            return "telescope.resource";
        }

        if (element.type === ResourceNodeType.ResourceGroup) {
            return "telescope.group";
        }

        return undefined;
    }
}
