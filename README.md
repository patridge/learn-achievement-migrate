## Learn achievement migration tool

This program will migrate any **type: badge** achievements from a Microsoft Learn repo's root achievements.yml file to their respective module index.yml files. Any badge achievements that cannot be matched to a module will be considered to be from modules that were removed/deprecated and remain in the achievements.yml file.

(Since this program is a .NET Core 2.0 application, it should work on Linux or macOS with the SDK installed, but it hasn't been tested there. As many of the path operations as possible were done to be agnostic of path separators, though.)

### Install this tool

Prerequisite: [.NET Core SDK](https://dotnet.microsoft.com/learn/dotnet/hello-world-tutorial#install)

1. Clone or download-and-extract this repo.
1. From a command line, navigate (e.g., `cd learn-achievement-migrate\`) into the repo.
1. Run `dotnet restore` to make sure you have any required NuGet packages downloaded.
1. Determine the path to your repo's achievements.yml file (e.g., `C:\dev\learn-pr\`).
1. Determine the path of the directory containing the modules you wish to migrate into (e.g., `C:\dev\learn-pr\learn-pr\azure\`). (Note: if you have multiple module folders, you currently have to execute this tool against each one separately.)
1. Execute the tool with the appropriate parameters, resulting in a command similar to the following.

    * `--achievements="path\to\achievements.yml"`
    * `--modules="\path\to\modules"`
  
    ```powershell
    dotnet run --achievements="C:\dev\learn-windows-pr\learn-windows-pr\achievements.yml" --modules="C:\dev\learn-windows-pr\learn-windows-pr\windows"
    ```
    
    This will result in edits to various matched modules and a new file: achievements-proposed.yml. This is the modified result of pulling lines from achievements.yml to put them in modules.

1. To use the new achievements-proposed.yml file, delete the original achievements.yml and rename the new one to take its place.
1. Stage and commit your work to a branch and submit a pull request.
