## Learn achievement migration tool

This program will migrate any achievements from a Microsoft Learn repo's primary achievements.yml file to their respective module and learning path index.yml files. Any achievements that cannot be matched to a module or learning path will be considered to be from removed/deprecated items and remain in the achievements.yml file.

(Since this program is a .NET Core 2.0 application, it should work on Linux or macOS with the SDK installed, but it hasn't been tested there. As many of the path operations as possible were done to be agnostic of path separators, though.)

### Install this migration tool

Prerequisite: [.NET Core SDK](https://dotnet.microsoft.com/learn/dotnet/hello-world-tutorial#install)

1. Clone or download-and-extract this repo.

### Run the migration tool (easy mode)

Assuming your Learn-style repo is structured as expected, you can provide only the path to the achievements.yml file.

1. From a command line, navigate into the tool repo (e.g., `cd learn-achievement-migrate\`).
1. Determine the path to your repo's achievements.yml file (e.g., `C:\dev\learn-pr\learn-pr\achievements.yml`).
1. Execute the tool with the appropriate parameters, resulting in a command similar to the following.

    ```powershell
    dotnet run --achievements="C:\dev\learn-windows-pr\learn-windows-pr\achievements.yml"
    ```

    This will result in edits to various matched modules and learning paths as well as a new file: achievements-proposed.yml. This is the modified result of pulling lines from achievements.yml to put them in modules and learning paths.

1. To use the new achievements-proposed.yml file, delete the original achievements.yml and rename the new one to take its place.
1. Stage and commit your work to a branch and submit a pull request.

### Run the migration tool (custom mode)

If you need to specify the various paths where modules can hide and/or the path to the learning paths, you can also provide several other parameters.

1. From a command line, navigate into the tool repo (e.g., `cd learn-achievement-migrate\`).
1. Determine the path to your repo's achievements.yml file (e.g., `C:\dev\learn-pr\learn-bizapps-pr\achievements.yml`).
1. Determine the paths of the directories containing the modules you wish to migrate into, separated by a semicolon for multiple paths (e.g., `C:\dev\learn-bizapps-pr\learn-bizapps-pr\dyn365-customer-service;C:\dev\learn-bizapps-pr\learn-bizapps-pr\dyn365-field-service;...`).
1. Determine the paths of the directory containing the learning paths you wish to migrate into (e.g., `C:\dev\learn-bizapps-pr\learn-bizapps-pr\paths\`).
1. Execute the tool with the appropriate parameters, resulting in a command similar to the following.

    * `--achievements="path\to\achievements.yml"`
    * `--modules="first\path\to\modules\;second\path\to\modules\;"`
    * `--learningPaths="path\to\learning\paths\"`

    Here's an example running against the learn-bizapps-pr repo:

    ```powershell
    dotnet run --achievements="C:\dev\learn-bizapps-pr\learn-bizapps-pr\achievements.yml" --modules="C:\dev\learn-bizapps-pr\learn-bizapps-pr\dyn365-customer-service;C:\dev\learn-bizapps-pr\learn-bizapps-pr\dyn365-field-service;C:\dev\learn-bizapps-pr\learn-bizapps-pr\flow;C:\dev\learn-bizapps-pr\learn-bizapps-pr\powerapps;C:\dev\learn-bizapps-pr\learn-bizapps-pr\power-bi;" --learningPaths="C:\dev\learn-bizapps-pr\learn-bizapps-pr\paths"
    ```

    This will result in edits to various matched modules and learning paths as well as a new file: achievements-proposed.yml. This is the modified result of pulling lines from achievements.yml to put them in modules and learning paths.

1. To use the new achievements-proposed.yml file, delete the original achievements.yml and rename the new one to take its place.
1. Stage and commit your work to a branch and submit a pull request.
