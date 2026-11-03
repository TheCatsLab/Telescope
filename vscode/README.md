# Telescope for Azure

Fast search across your Azure **Logic Apps, App Services and Function Apps** and their configuration — the Visual Studio Code port of the [Telescope extension for Visual Studio](../README.md) from this repository.

If you have a lot of Azure resources, Telescope makes it easier to find the one you need, inspect its configuration, copy what matters or jump straight to it in the Azure portal.

## Features

### Search and filtering
- `Telescope: Search Resources` (or **Alt+Shift+T**) opens a live search over all your subscriptions
- Filter by **resource name, tags and environment variables (app settings)**; add the **Configuration JSON** scope in the filter (`$(filter)`) button when you need to search inside the configuration itself
- **Literal text by default**; toggle `.*` for regular expressions — a broken regex safely falls back to a literal search
- Match counters per scope next to every result (`name ×1 · tag keys ×2`), mirrored as badges on the details tabs
- Accept a result to reveal it in the tree and open its details

### Resources
- **Logic Apps, App Services and Function Apps** grouped by tenant, subscription and resource group
- App settings are shown **masked by default** — reveal them with *Show values*
- Deployment slots are on the roadmap (they ship as child nodes, like in the Visual Studio extension)

### Details pane
- **Configuration JSON**, **Tags** and **Environment variables** tabs with match-count badges
- Copy the resource name, the full **ARM ID** or the configuration; multi-select tree nodes and copy the whole list
- Open the resource in the Azure portal

## Authentication
The extension authenticates through the **Azure CLI**:

```
az login
```

then run `Telescope: Sign In to Azure` (or just refresh the tree). If the CLI is missing, the sign-in message links to the installer.

## Requirements
- Visual Studio Code 1.90 or newer
- [Azure CLI](https://aka.ms/GetTheAzureCli) signed in (`az login`)
- An Azure account with permissions to read the subscriptions you want to browse

## Settings
| Setting | Default | Description |
| --- | --- | --- |
| `telescope.filter.useRegex` | `false` | Treat the search text as a regular expression instead of literal text |
| `telescope.filter.matchCase` | `false` | Match the search text case-sensitively |
| `telescope.filter.searchConfigurationJson` | `false` | Search inside the resource Configuration JSON as well (slower) |

The filter buttons in the search update these settings, so your toggles stick between sessions — like the Visual Studio extension.

## Development
Open the `vscode/` folder (this folder) in VS Code and:

```
npm install
npm run compile   # type-check + bundle to dist/
npm test          # the filter engine suite (a port of the xUnit tests)
```

Press **F5** to launch the Extension Development Host, or `npm run package` to produce a `.vsix`.

## Roadmap
- Deployment slots as child nodes (configuration, tags and app settings per slot)
- Search history
- Match highlighting in the details pane

## License
[MIT](LICENSE) — © The Cats Dev Lab
