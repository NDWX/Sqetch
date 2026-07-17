namespace Pug.Sqetch.Bundling.Layouts;

public static class IBundleLayoutRegistryExtensions
{
    public static void RegisterLayouts(this IBundleLayoutRegistry registry)
    {
        registry.Register( DefaultBundleLayout.LayoutName, () => new DefaultBundleLayout() );
    }
}