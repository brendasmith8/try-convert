# dotnet try-convert

| |Unit Tests (Debug)|Unit Tests (Release)|
|---|:--:|:--:|
| ci |[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/try-convert/try-convert-ci?branchName=master&jobName=Windows_NT&configuration=Windows_NT%20Debug&label=master)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=616&branchName=master)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/try-convert/try-convert-ci?branchName=master&jobName=Windows_NT&configuration=Windows_NT%20Release&label=master)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=616&branchName=master)|
| official | [![Build Status](https://dev.azure.com/dnceng/internal/_apis/build/status/dotnet/try-convert/try-convert-official?branchName=master&jobName=Windows_NT&configuration=Windows_NT%20Debug&label=master)](https://dev.azure.com/dnceng/internal/_build/latest?definitionId=615&branchName=master)|[![Build Status](https://dev.azure.com/dnceng/internal/_apis/build/status/dotnet/try-convert/try-convert-official?branchName=master&jobName=Windows_NT&configuration=Windows_NT%20Release&label=master)](https://dev.azure.com/dnceng/internal/_build/latest?definitionId=615&branchName=master)|

This is a tool that will help in migrating .NET Framework projects to .NET Core (or .NET SDK-style if you're not ready for .NET Core yet).

As the name suggests, this tool is not guaranteed to fully convert a project into a 100% working state. The tool is conservative and does as good of a job as it can to ensure that a converted project can still be loaded into Visual Studio and build. However, there are an enormous amount of factors that can result in a project that may not load or build that this tool explicitly does not cover.

It is highly recommended that you use this tool on a project that is under source control.

##  What does the tool do?

It loads a given project and evaluates it to get a list of all properties and items. It then replaces the project in memory with a simple .NET SDK based template and then re-evaluates it.

It does the second evaluation in the same project folder so that items that are automatically picked up by globbing will be known as well. It then applies rules about well-known properties and items, finally producing a diff of the two states to identify the following:

- Properties that can now be removed from the project because they are already implicitly defined by the SDK and the project had the default value.
- Properties that need to be kept in the project either because they override the default or it's a property not defined in the SDK.
- Items that can be removed because they are implicitly brought in by globs in the SDK
- Items that need to be changed to the Update syntax because although they're brought by the SDK, there is extra metadata being added.
- Items that need to be kept because they are are not implicit in the SDK.

This diff is used to convert a given project file.

## Attribution

This tool is based on the work of [Srivatsn Narayanan](https://github.com/srivatsn) and his [ProjectSimplifier](https://github.com/srivatsn/ProjectSimplifier) project.
