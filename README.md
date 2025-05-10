# CraftSharp Resource
A Unity package for loading resource files from Minecraft JE. 

## > Contribution
Unity packages cannot be directly opened as a project in Unity, instead you need to open it as an 'embedded package' inside a project in order to make changes. You can fork the project, and clone the fork into <code>SomeUnityProject/Packages/com.devbobcorn.craftsharp-resource</code>. Then you can open your <code>SomeUnityProject</code> from Unity Hub and find it in Unity's project explorer.

## > Usage
This package provides utility classes for loading resource files from Minecraft:Java Edition, for example, textures and json models for blocks and items. This package depends on [CraftSharp](https://github.com/DevBobcorn/CraftSharp) which provides basic data for the game.

To add this package as a dependency of your project, open Package Manager window in Unity, click the '+' symbol on upper-left corner, select 'Add package from git URL', and then use '[https://github.com/DevBobcorn/CraftSharp-Resource.git]()' (with the '.git' suffix) as the target url.

Check out [CornCraft](https://github.com/DevBobcorn/CornCraft) to see an example of using this package.

## > License
Most code in this repository is open source under CDDL-1.0, and this license applies to all source code except those mention their author and license or with specific license attached.

Some other open-source projects/code examples are used in the project, which use their own licenses. Here's a list of them:
* [Minecraft-Console-Client](https://github.com/MCCTeam/Minecraft-Console-Client) (Json Parser code)

The full CDDL-1.0 license can be reviewed [here](./LICENSE.md).