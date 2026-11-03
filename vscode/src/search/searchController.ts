import * as vscode from "vscode";

import { DetailsPanel } from "../details/detailsPanel";
import { FilterBy, defaultFilterByOptions, filterByLabels, hasFlag } from "../filtering/filterBy";
import { NodeFilter } from "../filtering/nodeFilter";
import { NodeFilterMatcher } from "../filtering/nodeFilterMatcher";
import type { FilterMatch } from "../filtering/nodeFilterResult";
import { ResourceNode, ResourceNodeType } from "../models/resourceNode";
import type { TelescopeTreeProvider } from "../tree/telescopeTreeProvider";

interface SearchItem extends vscode.QuickPickItem {
    node?: ResourceNode;
}

interface SearchState {
    useRegex: boolean;
    matchCase: boolean;
    scopes: number;
}

/** How many items are listed for an empty query; typing filters the whole index */
const MAX_UNFILTERED_ITEMS = 200;

/**
 * The Telescope search: a QuickPick that live-filters the loaded resource
 * index with the ported NodeFilter engine, mirroring the search box of the
 * Visual Studio tool window (literal by default, `.*` regex toggle, scopes).
 */
export class SearchController {
    private index: ResourceNode[] | undefined;
    private indexPromise: Promise<ResourceNode[]> | undefined;

    constructor(
        private readonly provider: TelescopeTreeProvider,
        private readonly treeView: vscode.TreeView<ResourceNode>,
        private readonly extensionUri: vscode.Uri,
    ) {}

    /** Forgets the loaded resources; the next search reloads them */
    public invalidate(): void {
        this.index = undefined;
    }

    public async showSearch(): Promise<void> {
        let index: ResourceNode[];

        try {
            index = await this.ensureIndex();
        } catch (error) {
            vscode.window.showWarningMessage(error instanceof Error ? error.message : String(error));
            return;
        }

        if (index.length === 0) {
            vscode.window.showInformationMessage("Telescope: no Logic Apps, App Services or Function Apps were found.");
            return;
        }

        const config = vscode.workspace.getConfiguration("telescope.filter");
        const state: SearchState = {
            useRegex: config.get<boolean>("useRegex", false),
            matchCase: config.get<boolean>("matchCase", false),
            scopes: defaultFilterByOptions
                | (config.get<boolean>("searchConfigurationJson", false) ? FilterBy.ResourceData : 0),
        };

        const quickPick = vscode.window.createQuickPick<SearchItem>();
        quickPick.placeholder = "Search resources by name, tags, environment variables…";
        quickPick.matchOnDescription = false;
        quickPick.matchOnDetail = false;
        quickPick.ignoreFocusOut = false;
        quickPick.buttons = [
            scopesButton,
            regexButton,
            caseButton,
        ];
        this.applyTitle(quickPick, state);

        let debounce: ReturnType<typeof setTimeout> | undefined;

        const refilter = () => {
            if (debounce) {
                clearTimeout(debounce);
            }
            debounce = setTimeout(() => this.applyFilter(quickPick, index, state), 100);
        };

        quickPick.onDidChangeValue(refilter);
        quickPick.onDidTriggerButton((button) => {
            if (button === regexButton) {
                state.useRegex = !state.useRegex;
                vscode.workspace.getConfiguration("telescope.filter").update("useRegex", state.useRegex, vscode.ConfigurationTarget.Global);
            } else if (button === caseButton) {
                state.matchCase = !state.matchCase;
                vscode.workspace.getConfiguration("telescope.filter").update("matchCase", state.matchCase, vscode.ConfigurationTarget.Global);
            } else if (button === scopesButton) {
                void this.pickScopes(state.scopes).then((scopes) => {
                    if (scopes !== undefined) {
                        state.scopes = scopes;
                        const includeConfig = hasFlag(scopes, FilterBy.ResourceData);
                        vscode.workspace.getConfiguration("telescope.filter").update("searchConfigurationJson", includeConfig, vscode.ConfigurationTarget.Global);
                        this.applyTitle(quickPick, state);
                        refilter();
                    }
                });
                return;
            }

            this.applyTitle(quickPick, state);
            refilter();
        });
        quickPick.onDidAccept(() => {
            const chosen = quickPick.selectedItems[0];
            const filter = this.currentFilter(quickPick.value, state);
            quickPick.hide();

            if (chosen?.node) {
                void this.revealAndOpen(chosen.node, filter);
            }
        });
        quickPick.onDidHide(() => quickPick.dispose());

        this.applyFilter(quickPick, index, state);
        quickPick.show();
    }

    private async ensureIndex(): Promise<ResourceNode[]> {
        if (this.index) {
            return this.index;
        }

        if (!this.indexPromise) {
            this.indexPromise = this.buildIndex().finally(() => {
                this.indexPromise = undefined;
            });
        }

        return this.indexPromise;
    }

    /**
     * Loads the whole resource tree through the tree provider, so the index
     * nodes are the real tree nodes: search results can be revealed directly.
     */
    private async buildIndex(): Promise<ResourceNode[]> {
        const roots = await this.provider.getChildren();

        if (roots.some((node) => node.type === ResourceNodeType.SignIn)) {
            throw new Error("Telescope: sign in to Azure first (Telescope: Sign In to Azure).");
        }

        const controller = new AbortController();

        return vscode.window.withProgress(
            {
                location: vscode.ProgressLocation.Notification,
                title: "Telescope: loading resources…",
                cancellable: true,
            },
            async (progress, token) => {
                token.onCancellationRequested(() => controller.abort());
                const leaves: ResourceNode[] = [];

                for (const tenant of roots) {
                    if (controller.signal.aborted) {
                        break;
                    }

                    progress.report({ message: tenant.label });

                    const subscriptions = (await this.provider.getChildren(tenant))
                        .filter((node) => node.type === ResourceNodeType.Subscription);

                    for (const subscription of subscriptions) {
                        if (controller.signal.aborted) {
                            break;
                        }

                        progress.report({ message: `${tenant.label} › ${subscription.label}` });

                        const groups = (await this.provider.getChildren(subscription))
                            .filter((node) => node.type === ResourceNodeType.ResourceGroup);

                        for (const group of groups) {
                            if (controller.signal.aborted) {
                                break;
                            }

                            progress.report({ message: `${subscription.label} › ${group.label}` });

                            const apps = (await this.provider.getChildren(group))
                                .filter((node) => this.isResourceLeaf(node));

                            leaves.push(...apps);
                        }
                    }
                }

                this.index = leaves;
                return leaves;
            },
        );
    }

    private isResourceLeaf(node: ResourceNode): boolean {
        return node.type === ResourceNodeType.LogicApp
            || node.type === ResourceNodeType.Function
            || node.type === ResourceNodeType.WebService;
    }

    private currentFilter(value: string, state: SearchState): NodeFilter | undefined {
        if (!value) {
            return undefined;
        }

        return new NodeFilter({
            searchText: value,
            filterByOptions: state.scopes,
            ignoreCase: !state.matchCase,
            isRegex: state.useRegex,
        });
    }

    private applyFilter(quickPick: vscode.QuickPick<SearchItem>, index: ResourceNode[], state: SearchState): void {
        const filter = this.currentFilter(quickPick.value, state);

        if (!filter) {
            const items = index.slice(0, MAX_UNFILTERED_ITEMS).map((node) => this.toItem(node));
            if (index.length > MAX_UNFILTERED_ITEMS) {
                items.push({
                    label: `…and ${index.length - MAX_UNFILTERED_ITEMS} more. Type to filter.`,
                    alwaysShow: true,
                });
            }
            quickPick.items = items;
            return;
        }

        const items: SearchItem[] = [];

        for (const node of index) {
            const result = NodeFilterMatcher.matchesCount(node.id, node.data, node.tags, node.environmentVariables, filter);

            if (!result.success) {
                continue;
            }

            const item = this.toItem(node);
            item.detail = result.matches.map((match) => `${filterByLabels[match.by]} ×${match.count}`).join("   ·   ");
            items.push(item);
        }

        quickPick.items = items.length > 0
            ? items
            : [{ label: "No matching resources", alwaysShow: true }];
    }

    private toItem(node: ResourceNode): SearchItem {
        const subscription = node.parent?.parent?.label;
        const resourceGroup = node.parent?.label;

        return {
            node,
            label: node.label,
            description: [subscription, resourceGroup].filter(Boolean).join("  ›  "),
            iconPath: this.iconOf(node),
        };
    }

    private iconOf(node: ResourceNode): vscode.Uri {
        const icon = (name: string) => vscode.Uri.joinPath(this.extensionUri, "resources", "icons", name);

        switch (node.type) {
            case ResourceNodeType.LogicApp:
                return icon("Logic-Apps.svg");
            case ResourceNodeType.Function:
                return icon("Function-Apps.svg");
            default:
                return icon("App-Services.svg");
        }
    }

    private applyTitle(quickPick: vscode.QuickPick<SearchItem>, state: SearchState): void {
        const mode = state.useRegex ? "Regex" : "Literal";
        const caseMode = state.matchCase ? "match case" : "ignore case";
        const scopes = hasFlag(state.scopes, FilterBy.ResourceData)
            ? "name, tags, env vars, configuration JSON"
            : "name, tags, env vars";

        quickPick.title = `${mode} · ${caseMode} · searching: ${scopes}`;
    }

    private async pickScopes(current: number): Promise<number | undefined> {
        const entries: Array<{ label: string; flag: number; picked: boolean }> = [
            { label: "Resource name", flag: FilterBy.ResourceName, picked: hasFlag(current, FilterBy.ResourceName) },
            { label: "Configuration JSON (slower)", flag: FilterBy.ResourceData, picked: hasFlag(current, FilterBy.ResourceData) },
            { label: "Tag keys", flag: FilterBy.ResourceTagKeys, picked: hasFlag(current, FilterBy.ResourceTagKeys) },
            { label: "Tag values", flag: FilterBy.ResourceTagValues, picked: hasFlag(current, FilterBy.ResourceTagValues) },
            { label: "Environment variable keys", flag: FilterBy.ResourceEnvironmentVariableKeys, picked: hasFlag(current, FilterBy.ResourceEnvironmentVariableKeys) },
            { label: "Environment variable values", flag: FilterBy.ResourceEnvironmentVariableValues, picked: hasFlag(current, FilterBy.ResourceEnvironmentVariableValues) },
        ];

        const picked = await vscode.window.showQuickPick(entries, {
            canPickMany: true,
            title: "Telescope: search scopes",
            placeHolder: "Choose which resource parts to search in",
        });

        if (!picked) {
            return undefined; // dismissed - keep the current scopes
        }

        return picked.reduce((scopes, entry) => scopes | entry.flag, 0);
    }

    private async revealAndOpen(node: ResourceNode, filter: NodeFilter | undefined): Promise<void> {
        await this.provider.ensureAncestorsLoaded(node);

        try {
            await this.treeView.reveal(node, { select: true, focus: true, expand: true });
        } catch {
            // the node might not be revealable (e.g. a filtered tree) - the details still open
        }

        const matches: FilterMatch[] | undefined = filter
            ? NodeFilterMatcher.matchesCount(node.id, node.data, node.tags, node.environmentVariables, filter).matches
            : undefined;

        DetailsPanel.open(node, matches, this.extensionUri);
    }
}

const scopesButton: vscode.QuickInputButton = {
    iconPath: new vscode.ThemeIcon("filter"),
    tooltip: "Choose which resource parts to search in",
};

const regexButton: vscode.QuickInputButton = {
    iconPath: new vscode.ThemeIcon("regex"),
    tooltip: "Treat the search text as a regular expression (invalid patterns fall back to literal)",
};

const caseButton: vscode.QuickInputButton = {
    iconPath: new vscode.ThemeIcon("case-sensitive"),
    tooltip: "Match case",
};
