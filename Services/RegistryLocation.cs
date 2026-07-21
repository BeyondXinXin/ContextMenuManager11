using ContextMenuManager11.Models;

namespace ContextMenuManager11.Services;

internal sealed record RegistryLocation(
    RegistryRoot Root,
    ContextMenuScope Scope,
    ContextMenuKind Kind,
    string SubKeyPath)
{
    public string FullPath => $@"{Root}\{SubKeyPath}";
}
