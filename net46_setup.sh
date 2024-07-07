#!/bin/bash

# Get the current directory
parent_directory=$(pwd)/mods

# Loop through each directory (excluding "dll" and ".vs")
for directory in "$parent_directory"/*; do
    if [ -d "$directory" ] && [ "$(basename "$directory")" != "dll" ] && [ "$(basename "$directory")" != ".vs" ]; then
        echo "Processing directory: $directory"
        cd "$directory" || continue
        dotnet add package Microsoft.NETFramework.ReferenceAssemblies.net46 --version 1.0.3
        cd "$parent_directory"
    fi
done