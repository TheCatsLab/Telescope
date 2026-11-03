import * as vscode from "vscode";

import { signIn } from "./azure/auth";
import { TelescopeService } from "./azure/telescopeService";
import { DetailsPanel } from "./details/detailsPanel";
import { ResourceNode, ResourceNodeType } from "./models/resourceNode";
import { SearchController } from "./search/searchController";
import { TelescopeTreeProvider } from "./tree/telescopeTreeProvider";

export function activate(context: vscode.ExtensionContext): void {
    const service = new TelescopeService();
    const provider = new TelescopeTreeProvider(service, context.extensionUri);
    const treeView = vscode.window.createTreeView("telescopeResources", {
        treeDataProvider: provider,
        canSelectMany: true,
        showCollapseAll: true,
    });
    const search = new SearchController(provider, treeView, context.extensionUri);

    // the cancel button of the view title is only visible while something is loading
    const loadingSubscription = service.onLoadingChange((isLoading) => {
        void vscode.commands.executeCommand("setContext", "telescope.isLoading", isLoading);
    });

    /** The resource nodes the tree commands act on: the context-menu node or the selection */
    const targetNodes = (node?: ResourceNode): ResourceNode[] => {
        const selection = treeView.selection.filter(isActableNode);
        if (node && isActableNode(node)) {
            return selection.includes(node) ? selection : [node];
        }
        return selection;
    };

    const singleTarget = (node?: ResourceNode): ResourceNode | undefined => {
        return targetNodes(node)[0];
    };

    const register = (command: string, handler: (...args: never[]) => unknown) => {
        context.subscriptions.push(vscode.commands.registerCommand(command, handler));
    };

    register("telescope.signIn", () => void signIn());

    register("telescope.refresh", () => {
        search.invalidate();
        provider.refresh();
    });

    register("telescope.cancelLoading", () => service.cancelLoading());

    register("telescope.search", () => void search.showSearch());

    register("telescope.openDetails", (node?: ResourceNode) => {
        const target = singleTarget(node);
        if (target) {
            DetailsPanel.open(target, undefined, context.extensionUri);
        }
    });

    register("telescope.copyName", (node?: ResourceNode) => {
        const nodes = targetNodes(node);
        if (nodes.length > 0) {
            void copyToClipboard(nodes.map((item) => item.label).join("\n"));
        }
    });

    register("telescope.copyArmId", (node?: ResourceNode) => {
        const nodes = targetNodes(node);
        if (nodes.length > 0) {
            void copyToClipboard(nodes.map((item) => item.armId ?? item.label).join("\n"));
        }
    });

    register("telescope.copyConfiguration", (node?: ResourceNode) => {
        const target = singleTarget(node);
        if (!target) {
            return;
        }

        if (target.data) {
            void copyToClipboard(target.data);
        } else {
            vscode.window.showWarningMessage(`Telescope: ${target.label} has no configuration JSON to copy.`);
        }
    });

    register("telescope.openInPortal", (node?: ResourceNode) => {
        const nodes = targetNodes(node);
        const links = nodes.map((item) => item.linkToResource).filter((link): link is string => !!link);

        if (links.length === 0) {
            vscode.window.showWarningMessage("Telescope: no Azure portal link for this node.");
            return;
        }

        for (const link of links) {
            void vscode.env.openExternal(vscode.Uri.parse(link));
        }
    });

    register("telescope.retryLoading", (node?: ResourceNode) => {
        provider.refresh(node);
    });

    context.subscriptions.push(treeView, loadingSubscription);
}

export function deactivate(): void {
    // nothing to clean up beyond the subscriptions
}

function isActableNode(node: ResourceNode): boolean {
    return node.type !== ResourceNodeType.Empty
        && node.type !== ResourceNodeType.Error
        && node.type !== ResourceNodeType.SignIn;
}

async function copyToClipboard(text: string): Promise<void> {
    await vscode.env.clipboard.writeText(text);
    vscode.window.showInformationMessage("Telescope: copied to the clipboard.");
}
