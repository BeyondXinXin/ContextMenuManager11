using ContextMenuManager11.Core.Models;

namespace ContextMenuManager11.Core.Registry;

public sealed record RegistryLocation(
    RegistryRoot Root,
    ContextMenuScope Scope,
    ContextMenuKind Kind,
    string SubKeyPath)
{
    public string FullPath => $@"{Root}\{SubKeyPath}";
    public string DisabledHandlersSubKeyPath => $@"{SubKeyPath}\DisabledContextMenuHandlers";
}
