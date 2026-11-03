# Telescope
Download this extension from the VS marketplace Visual Studio.

# Features
An extension for fast search of your Logic Apps, App Services, Function Apps (with their **deployment slots**) and their configuration across the Azure subscriptions. Telescope makes it easier to find a required resource if you have a lot.
You can easily find what you need, copy its content, or navigate to the resource in the Azure portal.

## Search and filtering
- Filter resources by **name, configuration JSON, tags and environment variables** — pick the scopes with quick chips or the *Options* popup
- **Literal text by default**; enable the `.*` toggle to use regular expressions (a broken regex safely falls back to literal search)
- *Match case* toggle, live filtering with background matching, search history
- A status line shows how many resources match; when nothing is found, Telescope suggests widening the search into the disabled scopes with one click
- Match counters on the tree nodes and result highlighting in the details pane and next to the scrollbars

## Resources
- **Logic Apps**, **App Services** and **Function Apps** grouped by subscriptions and resource groups
- **Deployment slots** are shown as child nodes of an app — expand an app to load them, with configuration, tags and app settings per slot
- App settings ("environment variables") are shown **masked by default** — reveal them with the *Show values* chip

## Details pane
- Configuration JSON with syntax highlighting and **Ctrl+F** search
- Tags and environment variables with search highlighting and match counters
- Actions: copy the resource name, the full **ARM ID**, the configuration or open the resource in the Azure portal

## Convenience
- Context menus everywhere: right-click a tree node, tag or environment variable to copy what you need
- Select multiple nodes with the checkboxes and copy the whole list to the clipboard
- **Refresh** and **Cancel loading** buttons; the tree stays interactive while the data loads
- Search history, window layout, grid splitter and the selected filter scopes are remembered between sessions

## Accessibility
- **Ctrl+E** focuses the search box, **Esc** clears the query
- All icon-only buttons have automation names; all colors follow the VS theme (dark, light and High Contrast)

## Authentication
The extension uses your account for connecting with Azure, so be sure your credentials are valid:
Tools -> Options -> Azure Service Authentication.

## Requirements
- Visual Studio 2022 (17.0) or Visual Studio 2026
- An Azure account with permissions to read the subscriptions you want to browse
