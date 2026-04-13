namespace Iris.Components.Theme;

public static partial class IrisIcons
{

    public static string Rounded => "material-symbols-rounded/";
        
    public static string Round(string icon) => Rounded + icon;
    
    public static string Filled => "material-symbols-filled/";
    
    public static string Fill(string icon) => Filled + icon;
    
    public static string Key => Round("key");

    public static string Account => Round("account_circle");

    public static string Connection => Round("power");
    
    public static string Connections => Round("link");


    public static string AddConnection => Round("power");
    
    //Custom FontAwesome Plug with Slash
    public static string NoConnections => $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 640 512\"><path d=\"M5.1 9.2C13.3-1.2 28.4-3.1 38.8 5.1L216 144l216 0 48 0 8 0c13.3 0 24 10.7 24 24s-10.7 24-24 24l-8 0 0 64c0 28.6-7.5 55.5-20.7 78.7L630.8 469.1c10.4 8.2 12.3 23.3 4.1 33.7s-23.3 12.3-33.7 4.1L9.2 42.9C-1.2 34.7-3.1 19.6 5.1 9.2zM160 222.1l48.1 37.9C210.1 320 259.4 368 320 368c7.7 0 15.2-.8 22.4-2.2l44.9 35.4c-13.5 6.3-28.1 10.7-43.3 13l0 73.8c0 13.3-10.7 24-24 24s-24-10.7-24-24l0-73.8c-77-11.6-136-78-136-158.2l0-33.9zM208 24c0-13.3 10.7-24 24-24s24 10.7 24 24l0 88-48 0 0-88zm69.3 168L420.9 304.6C428 289.9 432 273.4 432 256l0-64-154.7 0zM384 24c0-13.3 10.7-24 24-24s24 10.7 24 24l0 88-48 0 0-88z\"/></svg>";

    public static string Endpoints => Round("lan");

    public static string Endpoint => Round("signal-stream");
    
    //Custom FontAwesome Sitemap with Slash

    public static string NoEndpoints => $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 640 512\"><path d=\"M5.1 9.2C13.3-1.2 28.4-3.1 38.8 5.1L246.3 167.7c-4-7-6.3-15.1-6.3-23.7l0-64c0-26.5 21.5-48 48-48l64 0c26.5 0 48 21.5 48 48l0 64c0 26.5-21.5 48-48 48l-8 0 0 40 152 0c30.9 0 56 25.1 56 56l0 32 8 0c26.5 0 48 21.5 48 48l0 64c0 5.9-1.1 11.6-3 16.8l25.8 20.3c10.4 8.2 12.3 23.3 4.1 33.7s-23.3 12.3-33.7 4.1L9.2 42.9C-1.2 34.7-3.1 19.6 5.1 9.2zM32 368c0-26.5 21.5-48 48-48l8 0 0-32c0-30.9 25.1-56 56-56l28.6 0 60.9 48L144 280c-4.4 0-8 3.6-8 8l0 32 8 0c26.5 0 48 21.5 48 48l0 64c0 26.5-21.5 48-48 48l-64 0c-26.5 0-48-21.5-48-48l0-64zm48 0l0 64 64 0 0-64-64 0zm160 0c0-25.3 19.6-46.1 44.5-47.9L345.2 368 288 368l0 64 64 0 0-58.7 48 37.8 0 20.8c0 26.5-21.5 48-48 48l-64 0c-26.5 0-48-21.5-48-48l0-64zm35-177.8l21 16.5 0-14.7-8 0c-4.5 0-8.9-.6-13-1.8zM288 80l0 64 64 0 0-64-64 0zM389.5 280l71 55.7C469.3 326 482 320 496 320l8 0 0-32c0-4.4-3.6-8-8-8l-106.5 0zm63.1 172.6L486.1 479c-14.8-3.1-27.1-13-33.4-26.3zM501.8 368L560 413.6l0-45.6-58.2 0z\"/></svg>";

    public static string Packages => Round("package_2");

    //Custom FontAwesome Cubes with Slash
    public static string NoPackages =>
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 640 512\"><path d=\"M5.1 9.2C13.3-1.2 28.4-3.1 38.8 5.1l121.2 95 0-7.1c0-23.7 14.9-44.8 37.2-52.7l104-37.1c12.2-4.3 25.5-4.3 37.6 0l104 37.1C465.1 48.2 480 69.3 480 93l0 114.6 91.9 34.8c21.8 8.2 36.1 29.1 36.1 52.4l0 119.1c0 11-3.2 21.5-8.9 30.3l31.7 24.9c10.4 8.2 12.3 23.3 4.1 33.7s-23.3 12.3-33.7 4.1L9.2 42.9C-1.2 34.7-3.1 19.6 5.1 9.2zM32 294.7c0-23.3 14.4-44.1 36.1-52.4l79.4-30.1 56.6 44.6L187 250.7c-1.8-.6-3.7-.6-5.5 .1l-78.7 29.8L184 311.7l60.4-23.1 43.9 34.6L208 353.9l0 101.3 88-36.4 0-89.6L344 367l0 51.7c29.3 12.1 58.7 24.3 88 36.4l0-18.7 74.5 58.7-28.4 12.5c-14 6.1-29.8 6.3-43.9 .5L320 460.7 205.8 508c-14.1 5.8-30 5.7-43.9-.5L65.5 465.1c-20.4-8.9-33.5-29-33.5-51.2l0-119.2zm48 28.5l0 90.7c0 3.2 1.9 6 4.8 7.3L160 454.3l0-100.4L80 323.2zM208 118l0 19.7 88 69 0-55L208 118zm28.1-40.7L320 109.5l83.9-32.1-81.2-29 0-.1c-1.7-.6-3.6-.6-5.4 0l-81.2 29zM344 151.6l0 86.9c29.3-10.4 58.7-20.8 88-31.3l0-89.3-88 33.7zm39.7 123.8l21.4 16.8L456 311.7l81.2-31.1-78.7-29.8c-1.8-.7-3.7-.7-5.5-.1l-69.3 24.7zm98.8 77.4L560 413.6l0-90.5-77.5 29.7z\"/></svg>";

    public static string History => Round("history");

    public static string NoHistory =>
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 640 512\"><path d=\"M5.1 9.2C13.3-1.2 28.4-3.1 38.8 5.1L76.7 34.8C80.1 33 83.9 32 88 32c13.3 0 24 10.7 24 24l0 6.5 22.2 17.4C180.8 30.7 246.8 0 320 0C461.4 0 576 114.6 576 256c0 51-14.9 98.5-40.6 138.4l95.4 74.7c10.4 8.2 12.3 23.3 4.1 33.7s-23.3 12.3-33.7 4.1L9.2 42.9C-1.2 34.7-3.1 19.6 5.1 9.2zM64 146.4L121.8 192 88 192c-13.3 0-24-10.7-24-24l0-21.6zM172 436.2c7.4-11 22.3-14 33.3-6.7C238.1 451.3 277.5 464 320 464c39.7 0 76.9-11.1 108.4-30.4l39.7 31.3C426.3 494.5 375.2 512 320 512c-52.2 0-100.8-15.6-141.3-42.5c-11-7.4-14.1-22.3-6.7-33.3zm.2-326.5l123.8 97 0-54.7c0-13.3 10.7-24 24-24l-.1 0c13.3 0 24 10.7 24 24l0 92.2L497.5 364.6C516.8 333 528 295.8 528 256c0-114.9-93.1-208-208-208c-57.8 0-110.1 23.6-147.8 61.7z\"/></svg>";

    public static string CreateMessageData => Round("wand_stars");

    public static string Send => Round("send");

    public static string MoreOptions => Round("more_vert");

    public static string UploadPackage => Round("file_upload");

    public static string LightMode => Round("light_mode");

    public static string DarkMode => Round("dark_mode");

    public static string SystemMode => Round("laptop");

    public static string Settings => Round("settings");

    public static string Home => Round("house");

    public static string Format => Round("format_indent_increase");

    public static string AddTemplate => Round("file_export");
    
    public static string Success => Round("check_circle");

    public static string Error => Round("circle_x");

    public static string Info => Round("circle_info");

    public static string Add => Round("add");

    public static string RepeatSend => Round("repeat");

    public static string Delay => Round("schedule");
    
    public static string Templates => Round("files");

    public static string Duplicate => Round("content_copy_all");

    public static string Export => Round("download");

    public static string Preview => Round("visibility");

    public static string Support => Round("contact_support");

    public static string Bug => Round("bug_report");
    
    public static string Delete => Round("delete_forever");

    public static string Expand => Round("keyboard_double_arrow_right");

    public static string Collapse => Round("keyboard_double_arrow_left");

    public static string Visibility => Round("eye");

    public static string VisibilityOff => Round("eye_slash");

    public static string Editor => Round("edit_square");

    public static string Edit => Round("edit");

    public static string Import => Round("upload");

    public static string LoadToEditor => Round("arrow_insert");

    public static string ImportPackage => Round("deployed_code_update");

    public static string Refresh => Round("directory_sync");

    public static string Copy => Round("content_copy");

    public static string Help => Round("help");

    public static string Keyboard => Round("keyboard");

    public static string OpenInNew => Round("open_in_new");

    public static string Search => Round("search");

    public static string Back => Round("arrow_back");

    public static string ExpandMore => Round("expand_more");

    public static string ChevronRight => Round("chevron_right");
}