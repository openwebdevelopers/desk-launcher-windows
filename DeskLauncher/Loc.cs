using System.Globalization;
using System.Windows.Markup;

namespace DeskLauncher;

public static class LangCodes
{
    public const string System = "System";
    public const string English = "en";
    public const string Spanish = "es";
}

/// <summary>
/// UI localization. "System" follows the Windows UI language (Spanish if it is Spanish, English otherwise).
/// XAML uses <c>{local:Tr Key}</c> (evaluated when a window is created); code uses <see cref="Loc.T"/>.
/// </summary>
public static class Loc
{
    static string _lang = LangCodes.English;

    public static string Current => _lang;
    public static event Action? Changed;

    /// <summary>Resolves a setting ("System", "en", "es") to a supported language code.</summary>
    public static string Resolve(string? setting)
    {
        if (setting is LangCodes.English or LangCodes.Spanish) return setting;
        try
        {
            return CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "es" ||
                   CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es"
                ? LangCodes.Spanish : LangCodes.English;
        }
        catch { return LangCodes.English; }
    }

    public static void Apply(string? setting)
    {
        var lang = Resolve(setting);
        var changed = lang != _lang;
        _lang = lang;
        if (changed) Changed?.Invoke();
    }

    public static string T(string key) =>
        (_lang == LangCodes.Spanish ? Es : En).TryGetValue(key, out var v) ? v
        : En.TryGetValue(key, out var d) ? d : key;

    public static string T(string key, params object[] args) => string.Format(T(key), args);

    static readonly Dictionary<string, string> En = new()
    {
        ["Manager"] = "Manager",
        ["ActiveProfile"] = "Active profile",
        ["Navigation"] = "NAVIGATION",
        ["FoldersApps"] = "Folders & apps",
        ["Profiles"] = "Profiles",
        ["Settings"] = "Settings",
        ["FoldersAppsDesc"] = "Organize the desktop folders and the apps they contain.",
        ["NewFolder"] = "New folder",
        ["FoldersLabel"] = "FOLDERS",
        ["DeleteFolder"] = "Delete folder",
        ["DeleteFolderTip"] = "Delete selected folder",
        ["ShortcutsSuffix"] = " shortcuts",
        ["FolderName"] = "FOLDER NAME",
        ["ChangeIcon"] = "Change icon",
        ["ResetIcon"] = "Reset icon",
        ["Reset"] = "Reset",
        ["LocationStyle"] = "Location & style",
        ["LocationStyleDesc"] = "Where the folder appears and how its apps are shown in each place.",
        ["Desktop"] = "Desktop",
        ["Taskbar"] = "Taskbar",
        ["StyleArc"] = "Arc",
        ["StyleRing"] = "Ring",
        ["StylePhone"] = "Phone App",
        ["StyleBox"] = "Box",
        ["StyleList"] = "List",
        ["LinkedApps"] = "LINKED APPS",
        ["AddApp"] = "Add app…",
        ["AppIcon"] = "App icon",
        ["AppIconTip"] = "Change selected app's icon",
        ["AppResetTip"] = "Reset selected app's icon",
        ["DragToReorder"] = "Drag to reorder",
        ["RemoveFromFolder"] = "Remove from folder",
        ["NewProfile"] = "New profile",
        ["RenameProfile"] = "Rename profile",
        ["DeleteProfile"] = "Delete profile",
        ["ConfirmDeleteProfile"] = "Delete profile '{0}' and all its folders?",
        ["ProfilesDesc"] = "Switch the set of folders visible on the desktop.",
        ["AvailableProfiles"] = "AVAILABLE PROFILES",
        ["SettingsDesc"] = "General DeskLauncher preferences.",
        ["Appearance"] = "Appearance",
        ["AppearanceDesc"] = "System follows the Windows light/dark mode.",
        ["System"] = "System",
        ["Light"] = "Light",
        ["Dark"] = "Dark",
        ["Language"] = "Language",
        ["LanguageDesc"] = "System uses the Windows language (English if not detected).",
        ["AutoStart"] = "Start automatically with Windows",
        ["IconLibrary"] = "Icon library",
        ["OpenLibrary"] = "Open library",
        ["IconsAdded1"] = "1 icon added",
        ["IconsAddedN"] = "{0} icons added",
        ["ChooseApps"] = "Choose apps",
        ["FilterApps"] = "Apps and shortcuts|*.exe;*.lnk|All files|*.*",
        ["ProfileName"] = "Profile name",
        ["Cancel"] = "Cancel",
        ["Close"] = "Close",
        ["Select"] = "Select",
        ["PickIcon"] = "Choose icon",
        ["PickIconDesc"] = "Choose an icon or add a new one.",
        ["AddIcon"] = "Add icon…",
        ["RemoveSelected"] = "Remove selected",
        ["NoIcons"] = "No icons. Add images, .ico or .exe files.",
        ["Icons1"] = "1 icon",
        ["IconsN"] = "{0} icons",
        ["ConfirmRemoveIcon"] = "Remove this icon from the library?\nFolders and apps using it will go back to their default icon.",
        ["AddIcons"] = "Add icons",
        ["FilterIcons"] = "Icons and images|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.gif;*.exe;*.dll;*.lnk|All files|*.*",
        ["IconsFailed"] = "{0} file(s) could not be used as an icon.",
        ["OpenManager"] = "Open Manager",
        ["Profile"] = "Profile",
        ["Exit"] = "Exit",
        ["AppsStyle"] = "Shortcut style",
        ["ShowAlsoOnTaskbar"] = "Also show on the taskbar",
        ["ChangeIconDots"] = "Change icon…",
        ["ResetIconMenu"] = "Reset icon",
        ["ConfirmDeleteFolder"] = "Delete folder '{0}'?",
        ["Remove"] = "Remove",
        ["TaskbarStyle"] = "Taskbar style",
        ["ShowOnDesktop"] = "Show on desktop",
        ["RemoveFromTaskbar"] = "Remove from taskbar",
        ["LaunchFailed"] = "Could not open '{0}':\n{1}",
    };

    static readonly Dictionary<string, string> Es = new()
    {
        ["Manager"] = "Manager",
        ["ActiveProfile"] = "Perfil activo",
        ["Navigation"] = "NAVEGACIÓN",
        ["FoldersApps"] = "Carpetas y apps",
        ["Profiles"] = "Perfiles",
        ["Settings"] = "Configuración",
        ["FoldersAppsDesc"] = "Organiza las carpetas del escritorio y las apps que contiene.",
        ["NewFolder"] = "Nueva carpeta",
        ["FoldersLabel"] = "CARPETAS",
        ["DeleteFolder"] = "Eliminar carpeta",
        ["DeleteFolderTip"] = "Eliminar carpeta seleccionada",
        ["ShortcutsSuffix"] = " accesos",
        ["FolderName"] = "NOMBRE DE LA CARPETA",
        ["ChangeIcon"] = "Cambiar icono",
        ["ResetIcon"] = "Restablecer icono",
        ["Reset"] = "Restablecer",
        ["LocationStyle"] = "Ubicación y estilo",
        ["LocationStyleDesc"] = "Dónde aparece la carpeta y cómo se muestran sus apps en cada lugar.",
        ["Desktop"] = "Escritorio",
        ["Taskbar"] = "Barra de tareas",
        ["StyleArc"] = "Arco",
        ["StyleRing"] = "Anillo",
        ["StylePhone"] = "Phone App",
        ["StyleBox"] = "Caja",
        ["StyleList"] = "Lista",
        ["LinkedApps"] = "APLICACIONES VINCULADAS",
        ["AddApp"] = "Agregar app…",
        ["AppIcon"] = "Icono de app",
        ["AppIconTip"] = "Cambiar icono de la app seleccionada",
        ["AppResetTip"] = "Restablecer icono de la app seleccionada",
        ["DragToReorder"] = "Arrastra para cambiar el orden",
        ["RemoveFromFolder"] = "Quitar de la carpeta",
        ["NewProfile"] = "Nuevo perfil",
        ["RenameProfile"] = "Renombrar perfil",
        ["DeleteProfile"] = "Eliminar perfil",
        ["ConfirmDeleteProfile"] = "¿Eliminar el perfil '{0}' y todas sus carpetas?",
        ["ProfilesDesc"] = "Cambia el conjunto de carpetas visibles en el escritorio.",
        ["AvailableProfiles"] = "PERFILES DISPONIBLES",
        ["SettingsDesc"] = "Preferencias generales de DeskLauncher.",
        ["Appearance"] = "Apariencia",
        ["AppearanceDesc"] = "Sistema sigue el modo claro/oscuro de Windows.",
        ["System"] = "Sistema",
        ["Light"] = "Claro",
        ["Dark"] = "Oscuro",
        ["Language"] = "Idioma",
        ["LanguageDesc"] = "Sistema usa el idioma de Windows (inglés si no se detecta).",
        ["AutoStart"] = "Iniciar automáticamente con Windows",
        ["IconLibrary"] = "Biblioteca de iconos",
        ["OpenLibrary"] = "Abrir biblioteca",
        ["IconsAdded1"] = "1 icono agregado",
        ["IconsAddedN"] = "{0} iconos agregados",
        ["ChooseApps"] = "Elegir apps",
        ["FilterApps"] = "Apps y accesos directos|*.exe;*.lnk|Todos los archivos|*.*",
        ["ProfileName"] = "Nombre del perfil",
        ["Cancel"] = "Cancelar",
        ["Close"] = "Cerrar",
        ["Select"] = "Seleccionar",
        ["PickIcon"] = "Elegir icono",
        ["PickIconDesc"] = "Elige un icono o agrega uno nuevo.",
        ["AddIcon"] = "Agregar icono…",
        ["RemoveSelected"] = "Quitar seleccionado",
        ["NoIcons"] = "Sin iconos. Agrega imágenes, .ico o .exe.",
        ["Icons1"] = "1 icono",
        ["IconsN"] = "{0} iconos",
        ["ConfirmRemoveIcon"] = "¿Quitar este icono de la biblioteca?\nLas carpetas y apps que lo usan volverán a su icono por defecto.",
        ["AddIcons"] = "Agregar iconos",
        ["FilterIcons"] = "Iconos e imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.gif;*.exe;*.dll;*.lnk|Todos los archivos|*.*",
        ["IconsFailed"] = "{0} archivo(s) no se pudieron usar como icono.",
        ["OpenManager"] = "Abrir Manager",
        ["Profile"] = "Perfil",
        ["Exit"] = "Salir",
        ["AppsStyle"] = "Estilo de accesos",
        ["ShowAlsoOnTaskbar"] = "Mostrar también en la barra de tareas",
        ["ChangeIconDots"] = "Cambiar icono…",
        ["ResetIconMenu"] = "Restablecer icono",
        ["ConfirmDeleteFolder"] = "¿Eliminar la carpeta '{0}'?",
        ["Remove"] = "Quitar",
        ["TaskbarStyle"] = "Estilo en barra de tareas",
        ["ShowOnDesktop"] = "Mostrar en escritorio",
        ["RemoveFromTaskbar"] = "Quitar de la barra de tareas",
        ["LaunchFailed"] = "No se pudo abrir '{0}':\n{1}",
    };
}

/// <summary>XAML: <c>Text="{local:Tr Key}"</c>. Resolved when the element is created.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public string Key { get; set; }
    public TrExtension(string key) => Key = key;
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
