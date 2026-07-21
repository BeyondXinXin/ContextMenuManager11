namespace ContextMenuManager11.Services;

internal sealed class ClassicContextMenuService(RegistryEditService editService)
{
    public bool IsEnabled() => editService.IsClassicContextMenuEnabled();

    public void SetEnabled(bool enabled) => editService.SetClassicContextMenuEnabled(enabled);
}
