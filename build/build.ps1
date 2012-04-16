properties {
    $base_directory   = resolve-path "..\."
    $build_directory  = "$base_directory\release"
    $source_directory = "$base_directory\src"
    $tools_directory  = "$base_directory\tools"
    $version          = "1.2.1.1"
}

include .\psake_ext.ps1

task default -depends NuGet

task Clean -description "This task cleans up the build directory" {
    Remove-Item $build_directory\\$solution -Force -Recurse -ErrorAction SilentlyContinue
}

task Init -description "This tasks makes sure the build environment is correctly setup" {  
    Generate-Assembly-Info `
		-file "$source_directory\MefContrib\Properties\SharedAssemblyInfo.cs" `
		-title "MefContrib $version" `
		-description "Community-developed library of extensions to the Managed Extensibility Framework (MEF)." `
		-company "MefContrib" `
		-product "MefContrib $version" `
		-version $version `
		-copyright "Copyright © MefContrib 2009 - 2012" `
		-clsCompliant "false"
        
    if ((Test-Path $build_directory) -eq $false -or (Test-Path $build_directory\\$solution) -eq $false) {
        New-Item $build_directory\\$solution -ItemType Directory
    }
}

task Test -depends CopyArtifacts -description "This task executes all tests" {
    $previous_directory = pwd
    cd $build_directory\\$solution

    $testAssemblies = @(Get-ChildItem $build_directory\\$solution -Recurse -Include *.Tests.dll -Name | Split-Path -Leaf)
    
    foreach($assembly in $testAssemblies) {                   
        & $tools_directory\\nunit\\nunit-console.exe $assembly /nodots
        if ($lastExitCode -ne 0) {
            throw "Error: Failed to execute tests"
        }
    }
    
    cd $previous_directory 
}

task CopyArtifacts -depends Compile -description "This task copies all artifacts" {
    Get-ChildItem $source_directory -recurse -Include *.dll,*.pdb,*.config | where {$_ -notmatch 'packages' -and $_ -notmatch 'Debug'} | copy -destination $build_directory\\$solution
}

task Compile -depends Clean, Init -description "This task compiles the solution" {
    exec { 
        msbuild $source_directory\$solution.sln `
            /verbosity:quiet `
            /p:Configuration=Release `
			/property:WarningLevel=3
    }
}

task NuGet -depends Test -description "This task creates the NuGet packages" {
    Get-ChildItem $source_directory -recurse -Include *.nupkg | where {$_ -notmatch 'packages' -and $_ -notmatch 'Debug'} | copy -destination $build_directory
}