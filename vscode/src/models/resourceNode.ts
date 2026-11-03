/**
 * Port of src/Core/Enums/ResourceNodeType.cs
 */
export enum ResourceNodeType {
    Empty = "Empty",
    Error = "Error",
    GenericResource = "GenericResource",
    /** Not present in the C# original: the VS Code tree roots the browse at tenants */
    Tenant = "Tenant",
    Subscription = "Subscription",
    ResourceGroup = "ResourceGroup",
    LogicApp = "LogicApp",
    Function = "Function",
    WebService = "WebService",
    DeploymentSlot = "DeploymentSlot",
    /** UI-only node of the VS Code port: the sign-in call to action */
    SignIn = "SignIn",
}

/**
 * Presents a tree node for an Azure resource.
 * A lean port of src/Core/Models/ResourceNode.cs: the MVVM machinery of the
 * WPF original is dropped, the tree view handles expansion and selection.
 */
export class ResourceNode {
    /** Node id (it is an azure resource identifier segment as well) */
    public readonly id: string;

    /** Resource type */
    public readonly type: ResourceNodeType;

    /** The node display name (differs from the id for composite slot names) */
    public readonly label: string;

    /** Full azure resource manager identifier of the resource */
    public armId?: string;

    /** Node data (usually - json of the resource properties) */
    public data?: string | null;

    /** Resource tags */
    public tags?: Record<string, string> | null;

    /** Resource environment variables (app settings) */
    public environmentVariables?: Record<string, string> | null;

    /** The tenant of the subscription the resource belongs to (portal links) */
    public tenantId?: string;

    /** Child nodes; undefined means "not loaded yet" */
    public children: ResourceNode[] | undefined;

    /** Set when the last children load failed; the tree shows a retry item */
    public loadFailed = false;

    constructor(id: string, type: ResourceNodeType, label: string = id) {
        this.id = id;
        this.type = type;
        this.label = label;
    }

    /** Link to the resource in the Azure portal */
    public get linkToResource(): string | undefined {
        if (!this.armId || !this.tenantId) {
            return undefined;
        }

        return `https://portal.azure.com/#@${this.tenantId}/resource${this.armId}`;
    }

    /** Builds the ancestors chain: resource group, subscription, tenant */
    public ancestors(): ResourceNode[] {
        const chain: ResourceNode[] = [];
        for (let node: ResourceNode | undefined = this; node; node = node.parent) {
            chain.push(node);
        }
        return chain.reverse();
    }

    public parent: ResourceNode | undefined;
}
