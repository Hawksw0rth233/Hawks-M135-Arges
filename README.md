# M135 Arges for SPT

Version 1.0.0 targets SPT 4.1.6. Extract the release ZIP into the folder containing EscapeFromTarkov.exe.

## Build the plugins

With the .NET 10 SDK installed, run these commands from the repository root. Substitute your SPT installation directory.

    dotnet build Client/Hawks.Arges.Client.csproj -c Release "-p:SPTRoot=E:/SPT 4.1.6"
    dotnet build Server/Hawks.Arges.Server.csproj -c Release "-p:SPTRoot=E:/SPT 4.1.6"

Install the client DLL and Client/inspect-rotor.txt in BepInEx/plugins/Hawks-Arges. Install the server DLL and bundles.json in SPT_Runtime/user/mods/Hawks-Arges, with the two asset bundles under bundles/hawks.

## Unity assets

Editor contains the Unity asset build scripts and animation samples. Rebuilding the bundles requires the project-specific Unity 2022.3.43f1 WTT SDK assets, prepared prefabs, animation controllers, and PAYDAY 3 conversion inputs. These inputs and game libraries are external to this source repository. The scripts use paths relative to the Unity project root. Place the editor C# scripts in Assets/Editor; provide Editor/*.txt and the Client and bundles directories at the project root.

## License

The MIT license applies to original mod code. PAYDAY 3 and Escape from Tarkov models, animation data, audio, textures and other third-party assets remain the property of their respective owners.
