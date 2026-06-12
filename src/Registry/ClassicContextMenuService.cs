namespace ContextMenuManager11.Core.Registry;

public sealed class ClassicContextMenuService
{
    private readonly RegistryEditService _editService;

    public ClassicContextMenuService(RegistryEditService editService)
    {
        _editService = editService;
    }

    public bool IsEnabled() => _editService.IsClassicContextMenuEnabled();

    public void SetEnabled(bool enabled) => _editService.SetClassicContextMenuEnabled(enabled);
}
