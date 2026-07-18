using System.Runtime.CompilerServices;
using System.Windows;

// Allow the test project to exercise internal services (SecureStorage, PkceHelper, etc.).
[assembly: InternalsVisibleTo("WoWAddonIDE.Tests")]

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]
