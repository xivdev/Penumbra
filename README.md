# Penumbra

Penumbra is a runtime mod loader for FINAL FANTASY XIV, with a bunch of other useful features baked in:

* No need to back up your install - mods don't touch game files
* Disable and enable mods without restarting the game
* Resolve conflicts between mods by changing mod order
* Files can be edited and are often replicated in-game after a map change or closing and reopening a window

## Current Status
Penumbra, in its current state, is not intended for widespread use. It is mainly aimed at developers and people who don't need their hands held (for now).

We're working towards a 1.0 release, and you can follow it's progress [here](https://github.com/xivdev/Penumbra/projects/1).

## Contributing
Contributions are welcome, but please make an issue first before writing any code. It's possible what you want to implement is out of scope for this project, or could be reworked so that it would provide greater benefit.

## TexTools Mods
Penumbra has support for most TexTools modpacks however this is provided on a best-effort basis and support is not guaranteed. Built in tooling will be added to Penumbra over time to avoid many common TexTools use cases.

## Installing 
While this project is still a work in progress, you can use it by addin the following URL to the custom plugin repositories list in your Dalamud settings
1. `/xlsettings` -> Experimental tab
2. Copy and paste the repo.json link below
3. Click on the + button
4. Click on the "Save and Close" button
5. You will now see Penumbra listed in the Dalamud Plugin Installer

Please do not install Penumbra manually by downloading a release zip and unpacking it into your devPlugins folder. That will require manually updating Penumbra and you will miss out on features and bug fixes as you won't get update notifications automatically. Any manually installed copies of Penumbra should be removed before switching to the custom plugin respository method, as they will conflict.
- https://raw.githubusercontent.com/xivdev/Penumbra/master/repo.json
