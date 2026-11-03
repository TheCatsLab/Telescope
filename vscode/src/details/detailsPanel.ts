import * as vscode from "vscode";

import { filterByLabels } from "../filtering/filterBy";
import type { FilterMatch } from "../filtering/nodeFilterResult";
import { ResourceNode, ResourceNodeType } from "../models/resourceNode";

/**
 * The resource details pane of the VS Code port: a webview with the three
 * tabs of the WPF original - Configuration JSON, Tags and Environment
 * variables (app settings) - with copy actions and masked values.
 */
interface DetailsPayload {
    name: string;
    kindLabel: string;
    path: string;
    armId: string;
    portalUrl: string | null;
    configJson: string | null;
    tags: Record<string, string> | null;
    envVars: Record<string, string> | null;
    matchCounts: Record<string, number>;
}

const kindLabels: Readonly<Partial<Record<ResourceNodeType, string>>> = {
    [ResourceNodeType.LogicApp]: "Logic App",
    [ResourceNodeType.Function]: "Function App",
    [ResourceNodeType.WebService]: "App Service",
    [ResourceNodeType.DeploymentSlot]: "Deployment Slot",
};

export class DetailsPanel {
    private static readonly openPanels = new Map<string, DetailsPanel>();

    private readonly panel: vscode.WebviewPanel;

    private constructor(
        armId: string,
        payload: DetailsPayload,
        private readonly extensionUri: vscode.Uri,
    ) {
        this.panel = vscode.window.createWebviewPanel(
            "telescope.details",
            `${payload.name} — Telescope`,
            { viewColumn: vscode.ViewColumn.Beside, preserveFocus: false },
            {
                enableScripts: true,
                localResourceRoots: [vscode.Uri.joinPath(extensionUri, "resources")],
            },
        );

        this.panel.webview.html = this.renderHtml(payload);

        this.panel.webview.onDidReceiveMessage((message: { type: string; text?: string }) => {
            if (message.type === "copy" && message.text !== undefined) {
                vscode.env.clipboard.writeText(message.text);
                vscode.window.showInformationMessage("Telescope: copied to the clipboard.");
            } else if (message.type === "open" && message.text !== undefined) {
                vscode.env.openExternal(vscode.Uri.parse(message.text));
            }
        });

        this.panel.onDidDispose(() => DetailsPanel.openPanels.delete(armId));
    }

    public static open(node: ResourceNode, matches: FilterMatch[] | undefined, extensionUri: vscode.Uri): void {
        const armId = node.armId ?? node.id;
        const existing = DetailsPanel.openPanels.get(armId);

        if (existing) {
            existing.panel.title = `${node.label} — Telescope`;
            existing.panel.reveal();
            return;
        }

        const matchCounts: Record<string, number> = {};
        for (const match of matches ?? []) {
            const label = filterByLabels[match.by];
            matchCounts[label] = (matchCounts[label] ?? 0) + match.count;
        }

        const subscription = node.parent?.parent;
        const resourceGroup = node.parent;
        const payload: DetailsPayload = {
            name: node.label,
            kindLabel: kindLabels[node.type] ?? "Resource",
            path: [subscription?.label, resourceGroup?.label].filter(Boolean).join(" › "),
            armId,
            portalUrl: node.linkToResource ?? null,
            configJson: node.data ?? null,
            tags: node.tags ?? null,
            envVars: node.environmentVariables ?? null,
            matchCounts,
        };

        DetailsPanel.openPanels.set(armId, new DetailsPanel(armId, payload, extensionUri));
    }

    private renderHtml(payload: DetailsPayload): string {
        const nonce = this.generateNonce();
        const data = JSON.stringify(payload).replace(/</g, "\\u003c");

        return /* html */ `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; script-src 'nonce-${nonce}';">
    <style>
        body { font-family: var(--vscode-font-family); color: var(--vscode-foreground); padding: 0 16px 16px; }
        header { display: flex; align-items: baseline; gap: 10px; padding: 12px 0 8px; flex-wrap: wrap; }
        h1 { font-size: 1.2em; margin: 0; }
        .kind { color: var(--vscode-descriptionForeground); font-size: 0.9em; }
        .path { color: var(--vscode-descriptionForeground); font-size: 0.85em; margin-left: auto; }
        .toolbar { display: flex; gap: 8px; align-items: center; margin: 4px 0 12px; flex-wrap: wrap; }
        button {
            background-color: var(--vscode-button-secondaryBackground);
            color: var(--vscode-button-secondaryForeground);
            border: none; padding: 3px 10px; cursor: pointer; font-size: 0.85em; border-radius: 2px;
        }
        button:hover { background-color: var(--vscode-button-secondaryHoverBackground); }
        .tabs { display: flex; gap: 2px; border-bottom: 1px solid var(--vscode-panel-border); }
        .tabs button {
            background: transparent; color: var(--vscode-foreground);
            border: none; border-bottom: 2px solid transparent; padding: 6px 12px;
        }
        .tabs button.active { border-bottom-color: var(--vscode-focusBorder); font-weight: 600; }
        .badge {
            background-color: var(--vscode-badge-background); color: var(--vscode-badge-foreground);
            border-radius: 8px; padding: 0 6px; margin-left: 6px; font-size: 0.8em;
        }
        section { display: none; }
        section.active { display: block; }
        pre {
            font-family: var(--vscode-editor-font-family); font-size: var(--vscode-editor-font-size);
            background-color: var(--vscode-textCodeBlock-background); padding: 10px; border-radius: 4px;
            overflow: auto; max-height: 70vh; white-space: pre;
        }
        table { border-collapse: collapse; width: 100%; margin-top: 6px; }
        td, th { text-align: left; padding: 4px 10px 4px 0; border-bottom: 1px solid var(--vscode-panel-border); vertical-align: top; }
        th { color: var(--vscode-descriptionForeground); font-weight: 600; }
        td.value { font-family: var(--vscode-editor-font-family); word-break: break-all; }
        .masked { color: var(--vscode-descriptionForeground); letter-spacing: 2px; }
        .empty { color: var(--vscode-descriptionForeground); font-style: italic; padding: 10px 0; }
    </style>
</head>
<body>
    <header>
        <h1>${escapeHtml(payload.name)}</h1>
        <span class="kind">${escapeHtml(payload.kindLabel)}</span>
        <span class="path">${escapeHtml(payload.path)}</span>
    </header>
    <div class="toolbar">
        <button data-message="copy" data-text="${escapeAttribute(payload.armId)}">Copy ARM ID</button>
        ${payload.portalUrl ? `<button data-message="open" data-text="${escapeAttribute(payload.portalUrl)}">Open in Azure Portal</button>` : ""}
    </div>
    <nav class="tabs">
        <button data-tab="config" class="active">Configuration JSON${badge(payload.matchCounts["config"])}</button>
        <button data-tab="tags">Tags${badge((payload.matchCounts["tag keys"] ?? 0) + (payload.matchCounts["tag values"] ?? 0))}</button>
        <button data-tab="env">Environment variables${badge((payload.matchCounts["env keys"] ?? 0) + (payload.matchCounts["env values"] ?? 0))}</button>
    </nav>
    <section id="tab-config" class="active">
        <div id="config-content"></div>
    </section>
    <section id="tab-tags">
        <div id="tags-content"></div>
    </section>
    <section id="tab-env">
        <div class="toolbar">
            <button id="reveal-values">Show values</button>
            <button id="copy-env">Copy</button>
        </div>
        <div id="env-content"></div>
    </section>
    <script nonce="${nonce}">
        "use strict";
        const vscode = acquireVsCodeApi();
        const DATA = ${data};

        function el(id) { return document.getElementById(id); }
        function text(value) { return value === null || value === undefined || value === "" ? "" : String(value); }
        function empty(message) { return '<p class="empty">' + message + "</p>"; }

        // the copy buttons carry their payload in data-text
        for (const button of document.querySelectorAll("button[data-message]")) {
            button.addEventListener("click", () => vscode.postMessage({ type: button.dataset.message, text: button.dataset.text }));
        }

        // tab switching
        for (const tab of document.querySelectorAll(".tabs button")) {
            tab.addEventListener("click", () => {
                for (const other of document.querySelectorAll(".tabs button")) { other.classList.remove("active"); }
                for (const section of document.querySelectorAll("section")) { section.classList.remove("active"); }
                tab.classList.add("active");
                el("tab-" + tab.dataset.tab).classList.add("active");
            });
        }

        function renderTable(container, source, maskValues) {
            const entries = source ? Object.entries(source) : [];
            if (entries.length === 0) {
                container.innerHTML = empty("Nothing to show");
                return;
            }
            const rows = entries
                .map(([key, value]) =>
                    "<tr><th>" + escapeHtml(text(key)) + '</th><td class="value">' +
                    (maskValues ? '<span class="masked">••••••</span>' : escapeHtml(text(value)) || '<span class="masked">(empty)</span>') +
                    "</td></tr>")
                .join("");
            container.innerHTML = "<table><tr><th>Name</th><th>Value</th></tr>" + rows + "</table>";
        }

        function escapeHtml(value) {
            return value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#39;");
        }

        // Configuration JSON tab
        el("config-content").innerHTML = DATA.configJson
            ? "<pre>" + escapeHtml(DATA.configJson) + "</pre>"
            : empty("No configuration JSON available for this resource");

        // Tags tab
        const tagsEntries = DATA.tags ? Object.entries(DATA.tags) : [];
        el("tags-content").innerHTML = tagsEntries.length > 0
            ? "<div class=\\"toolbar\\"><button id=\\"copy-tags\\">Copy</button></div><div id=\\"tags-table\\"></div>"
            : empty("The resource has no tags");
        if (tagsEntries.length > 0) {
            renderTable(el("tags-table"), DATA.tags, false);
            el("copy-tags").addEventListener("click", () =>
                vscode.postMessage({ type: "copy", text: tagsEntries.map(([k, v]) => k + " = " + v).join("\\n") }));
        }

        // Environment variables tab - masked by default, like the VS extension
        let revealed = false;
        function renderEnv() { renderTable(el("env-content"), DATA.envVars, !revealed); }
        renderEnv();
        if (DATA.envVars && Object.keys(DATA.envVars).length > 0) {
            el("reveal-values").addEventListener("click", () => {
                revealed = !revealed;
                el("reveal-values").textContent = revealed ? "Hide values" : "Show values";
                renderEnv();
            });
            el("copy-env").addEventListener("click", () => {
                if (!revealed) {
                    vscode.postMessage({ type: "copy", text: Object.keys(DATA.envVars).join("\\n") });
                    return;
                }
                vscode.postMessage({ type: "copy", text: Object.entries(DATA.envVars).map(([k, v]) => k + " = " + v).join("\\n") });
            });
        } else {
            el("reveal-values").style.display = "none";
            el("copy-env").style.display = "none";
            el("env-content").innerHTML = empty("No environment variables (app settings) available");
        }
    </script>
</body>
</html>`;
    }

    private generateNonce(): string {
        const chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        let nonce = "";
        for (let i = 0; i < 32; i++) {
            nonce += chars[Math.floor(Math.random() * chars.length)];
        }
        return nonce;
    }
}

function badge(count: number | undefined): string {
    return count && count > 0 ? `<span class="badge">${count}</span>` : "";
}

function escapeHtml(value: string): string {
    return value
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function escapeAttribute(value: string): string {
    return escapeHtml(value);
}
