# Penumbra

Penumbra is a runtime mod loader for FINAL FANTASY XIV, with a bunch of other useful features baked in:

* No need to back up your install - mods don't touch game files
* Disable and enable mods without restarting the game
* Resolve conflicts between mods by changing mod order
* Files can be edited and are often replicated in-game after a map change or closing and reopening a window

## Support
Either open an issue here or join us in [Discord](https://discord.gg/kVva7DHV4r).

## Contributing
Contributions are welcome, but please make an issue first before writing any code. It's possible what you want to implement is out of scope for this project, or could be reworked so that it would provide greater benefit.

## TexTools Mods
Penumbra has support for most TexTools modpacks however this is provided on a best-effort basis and support is not guaranteed. Built in tooling will be added to Penumbra over time to avoid many common TexTools use cases.

## Installing 
While this project is still a work in progress, you can use it by adding the following URL to the custom plugin repositories list in your Dalamud settings
An image-based install (and usage) guide to do this is provided by unaffiliated user Serenity: https://reniguide.info/

1. `/xlsettings` -> Experimental tab
2. Copy and paste the repo.json link below
3. Click on the + button
4. Click on the "Save and Close" button
5. You will now see Penumbra listed in the Available Plugins tab in the Dalamud Plugin Installer
6. Do not forget to actually install Penumbra from this tab.

Please do not install Penumbra manually by downloading a release zip and unpacking it into your devPlugins folder. That will require manually updating Penumbra and you will miss out on features and bug fixes as you won't get update notifications automatically. Any manually installed copies of Penumbra should be removed before switching to the custom plugin respository method, as they will conflict.
- https://raw.githubusercontent.com/xivdev/Penumbra/master/repo.json
