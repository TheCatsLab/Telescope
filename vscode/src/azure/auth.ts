import { AzureCliCredential, type TokenCredential } from "@azure/identity";
import * as vscode from "vscode";

const ARM_SCOPE = "https://management.azure.com/.default";
const AZURE_CLI_INSTALL_URL = "https://aka.ms/GetTheAzureCli";

/**
 * Authentication for Telescope.
 *
 * The MVP authenticates through the Azure CLI (the user runs `az login`),
 * which needs no AAD application registration of our own. The credentials are
 * cached per tenant; ARM returns each subscription with its tenant id, so the
 * per-tenant tokens are minted transparently while the tree loads.
 *
 * Swapping in the built-in VS Code Microsoft auth provider later
 * (vscode.authentication.getSession("microsoft", [ARM_SCOPE]) - which needs an
 * own AAD client id) is a drop-in change of this module alone.
 */
const credentials = new Map<string, TokenCredential>();

export function getCredential(tenantId?: string): TokenCredential {
    const key = tenantId ?? "";

    if (!credentials.has(key)) {
        credentials.set(key, new AzureCliCredential(tenantId ? { tenantId } : undefined));
    }

    return credentials.get(key)!;
}

/** Forgets the cached credentials, e.g. after `az login` has switched the account */
export function resetCredentials(): void {
    credentials.clear();
}

/** Probes whether a management token can be minted for the default tenant */
export async function isAuthenticated(): Promise<boolean> {
    try {
        await getCredential().getToken(ARM_SCOPE);
        return true;
    } catch {
        return false;
    }
}

/**
 * The "Sign In to Azure" command: probes the CLI credential and guides the user
 * through `az login` / installing the Azure CLI when it is not available yet.
 */
export async function signIn(): Promise<void> {
    resetCredentials();

    if (await isAuthenticated()) {
        vscode.window.showInformationMessage("Telescope: signed in through the Azure CLI.");
        await vscode.commands.executeCommand("telescope.refresh");
        return;
    }

    const selection = await vscode.window.showErrorMessage(
        "Telescope: sign in with the Azure CLI first (run `az login`), or install the Azure CLI to continue.",
        "Run az login",
        "Install Azure CLI",
    );

    if (selection === "Run az login") {
        const terminal = vscode.window.createTerminal({ name: "Azure CLI" });
        terminal.show();
        terminal.sendText("az login");
        vscode.window.showInformationMessage("Telescope: run Telescope: Refresh after `az login` completes.");
    } else if (selection === "Install Azure CLI") {
        vscode.env.openExternal(vscode.Uri.parse(AZURE_CLI_INSTALL_URL));
    }
}
