module Program

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Http
open Giraffe
open Giraffe.ViewEngine
open Giraffe.ViewEngine.HtmlElements

type Message = { Text: string }

let logActivity (ctx: HttpContext) (action: string) (fileName: string option) =
    let user =
        match ctx.Request.Cookies.TryGetValue("role") with
        | true, r -> r
        | _ -> "unknown"
    let time = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss")
    let fileInfo = match fileName with Some name -> name | None -> ""
    let line = sprintf "[%s] %s | %s | %s\n" time user action fileInfo
    let logPath = Path.Combine(Directory.GetCurrentDirectory(), "activity.log")
    File.AppendAllText(logPath, line)

type Lang = | Hu | En

let parseLang (input: string) =
    match input.ToLowerInvariant() with
    | "en" -> En
    | _ -> Hu

let getLangFromContext (ctx: HttpContext) =
    let query = ctx.Request.Query
    if query.ContainsKey("lang") then
        parseLang (query.["lang"].ToString())
    else
        Hu

type Labels = {
    Title: string
    Upload: string
    UploadedFiles: string
    FileName: string
    FileDate: string
    FileSize: string
    FileType: string
    Download: string
    Delete: string
    ConfirmDelete: string
    Login: string
    Logout: string
    Register: string
    RegisterQ: string
    LoginFailed: string
    Username: string
    Password: string
    PasswordAgain: string
    UploadSection: string
    Operations: string
    DownloadOnlyLoggedIn: string
}

let labels lang =
    match lang with
    | Hu ->
        {
            Title = "Dokumentumkezelő"
            Upload = "⬆️ Feltöltés"
            UploadedFiles = "📂 Feltöltött fájlok"
            FileName = "Név"
            FileDate = "Dátum"
            FileSize = "Méret"
            FileType = "Típus"
            Download = "📥 Letöltés"
            Delete = "🗑️ Törlés"
            ConfirmDelete = "Biztosan törölni szeretnéd?"
            Login = "🔑 Bejelentkezés"
            Logout = "🚪 Kijelentkezés"
            Register = "Regisztráció"
            RegisterQ = "Nincs fiókod? Regisztrálj itt!"
            LoginFailed = "❌ Sikertelen bejelentkezés"
            Username = "Felhasználónév:"
            Password = "Jelszó:"
            PasswordAgain = "Jelszó ismét:"
            UploadSection = "Feltöltés"
            Operations = "Műveletek"
            DownloadOnlyLoggedIn = "🔒 Csak bejelentkezett felhasználók tölthetnek le fájlokat."
        }
    | En ->
        {
            Title = "Document Manager"
            Upload = "⬆️ Upload"
            UploadedFiles = "📂 Uploaded Files"
            FileName = "Name"
            FileDate = "Date"
            FileSize = "Size"
            FileType = "Type"
            Download = "📥 Download"
            Delete = "🗑️ Delete"
            ConfirmDelete = "Are you sure you want to delete?"
            Login = "🔑 Login"
            Logout = "🚪 Logout"
            Register = "Register"
            RegisterQ = "No account? Register here!"
            LoginFailed = "❌ Login failed"
            Username = "Username:"
            Password = "Password:"
            PasswordAgain = "Password again:"
            UploadSection = "Upload"
            Operations = "Operations"
            DownloadOnlyLoggedIn = "🔒 Only logged-in users can download files."
        }

module Views =
    open Giraffe.ViewEngine
    open Giraffe.ViewEngine.HtmlElements

    let formatFileSize (size: int64) : string =
        if size >= 1024L * 1024L then
            sprintf "%.2f MB" (float size / 1024.0 / 1024.0)
        elif size >= 1024L then
            sprintf "%.1f KB" (float size / 1024.0)
        else
            sprintf "%d B" size

    let getFileIcon (fileName: string) : string =
        match System.IO.Path.GetExtension(fileName).ToLower() with
        | ".pdf" -> "📕"
        | ".doc" | ".docx" -> "📄"
        | ".png" | ".jpg" | ".jpeg" -> "🖼️"
        | ".zip" | ".rar" -> "📦"
        | _ -> "📁"

    let isImage (ext: string) =
        match ext.ToLower() with
        | ".jpg" | ".jpeg" | ".png" | ".gif" | ".bmp" | ".webp" -> true
        | _ -> false

    let sortLink field currentSort currentDir lang =
        let newDir =
            if currentSort = field && currentDir = "asc" then "desc"
            else "asc"
        let langStr = match lang with | En -> "en" | Hu -> "hu"
        $"/?sort={field}&dir={newDir}&lang={langStr}"

    let langToggleButton (lang: Lang) =
        let (nextLang, flag, label) =
            match lang with
            | En -> ("hu", "🇭🇺", "Magyar")
            | Hu -> ("en", "🇬🇧", "English")
        button [
            _id "langToggleBtn"
            _type "button"
            _class "lang-toggle-btn"
            attr "data-nextlang" nextLang
        ] [ str (flag + " " + label) ]

    let fileList (files: (string * string * int64 * string) list) (role: string) (sort: string) (dir: string) (labels: Labels) (lang: Lang) : XmlNode =
        table [ _class "table" ] [
            thead [] [
                tr [] [
                    th [] [ a [ _href (sortLink "name" sort dir lang) ] [ str labels.FileName ] ]
                    th [] [ a [ _href (sortLink "date" sort dir lang) ] [ str labels.FileDate ] ]
                    th [] [ a [ _href (sortLink "size" sort dir lang) ] [ str labels.FileSize ] ]
                    th [] [ a [ _href (sortLink "type" sort dir lang) ] [ str labels.FileType ] ]
                    th [] [ str labels.Operations ]
                ]
            ]
            tbody [] [
                for (file, createdAt, size, extension) in files ->
                    tr [] [
                        td [] [
                            if isImage extension then
                                img [
                                    _src $"/uploads/{file}"
                                    _alt file
                                    attr "style" "max-width:40px; max-height:40px; margin-right:10px; border-radius:4px; vertical-align:middle;"
                                ]
                            str $"{getFileIcon file} {file}"
                        ]
                        td [] [ str createdAt ]
                        td [] [ str (formatFileSize size) ]
                        td [] [ str (extension.ToUpperInvariant()) ]
                        td [] [
                            if role <> "guest" then
                                a [ _href $"/download/{file}"; _class "download-btn" ] [ str labels.Download ]
                            if role = "admin" then
                                form [
                                    _action "/delete"
                                    _method "post"
                                    attr "onsubmit" (sprintf "return confirm('%s');" labels.ConfirmDelete)
                                    _style "display:inline;"
                                ] [
                                    input [ _type "hidden"; _name "fileName"; _value file ]
                                    input [ _type "submit"; _value labels.Delete; _class "delete-btn" ]
                                ]
                        ]
                    ]
            ]
        ]

    let uploadForm (labels: Labels) : XmlNode =
        form [
            _action "/upload"
            _method "post"
            attr "enctype" "multipart/form-data"
        ] [
            p [] [ input [ _type "file"; _name "file" ] ]
            p [] [
                input [
                    _type "submit"
                    _value labels.Upload
                    _class "upload-btn"
                ]
            ]
        ]

    let layout (role: string) (lang: Lang) (labels: Labels) (content: XmlNode list) : XmlNode =
        let authLinks =
            if role = "guest" || role = "" then
                [ a [ _href "/login"; _class "login-btn" ] [ str labels.Login ] ]
            else
                [ a [ _href "/logout"; _class "logout-btn" ] [ str labels.Logout ] ]
        let headerContent =
            [
                div [ _class "header" ] [
                    h1 [] [ str ("📁 " + labels.Title) ]
                    div [ _class "auth-link" ] (
                        [
                            button [
                                _id "darkModeToggle"
                                _type "button"
                                _class "darkmode-toggle"
                            ] [ str "🌗" ]
                        ] @
                        [ langToggleButton lang ] @
                        authLinks
                    )
                ]
            ]
        html [] [
            head [] [
                title [] [ str labels.Title ]
                meta [ attr "name" "viewport"; attr "content" "width=device-width, initial-scale=1.0" ]
                link [ _rel "stylesheet"; _href "/main.css" ]
            ]
            body [] (
                headerContent @ content @ [
                    script [] [
                        rawText """
                            const toggle = document.getElementById('darkModeToggle');
                            const body = document.body;
                            const current = localStorage.getItem('theme');
                            if (current === 'dark') body.classList.add('dark-mode');
                            toggle.addEventListener('click', () => {
                                body.classList.toggle('dark-mode');
                                const mode = body.classList.contains('dark-mode') ? 'dark' : 'light';
                                localStorage.setItem('theme', mode);
                            });

                            const langBtn = document.getElementById('langToggleBtn');
                            if (langBtn) {
                                langBtn.addEventListener('click', function() {
                                    let nextLang = langBtn.getAttribute('data-nextlang');
                                    let url = new URL(window.location.href);
                                    url.searchParams.set('lang', nextLang);
                                    window.location.href = url.toString();
                                });
                            }
                        """
                    ]
                ]
            )
        ]

    let index (model: Message) (files: (string * string * int64 * string) list) (role: string) (sort: string) (dir: string) (labels: Labels) (lang: Lang) : XmlNode =
        let adminSection =
            if role = "admin" then
                div [ _class "left" ] [
                    h2 [] [ str labels.UploadSection ]
                    p [] [ str model.Text ]
                    uploadForm labels
                    hr [] 
                    h3 [] [ str "Napló" ]
                    form [ _action "/download-log"; _method "get" ] [
                        input [ _type "submit"; _value "📥 Napló letöltése"; _class "download-btn" ]
                    ]
                    form [ _action "/delete-log"; _method "post"; attr "onsubmit" "return confirm('Biztosan törlöd a naplót?');" ] [
                        input [ _type "submit"; _value "🗑️ Napló törlése"; _class "delete-btn" ]
                    ]
                ]
            else
                emptyText
        layout role lang labels [
            div [ _class "container" ] [
                adminSection
                div [ _class "right" ] [
                    h2 [] [ str labels.UploadedFiles ]
                    fileList files role sort dir labels lang
                ]
            ]
        ]

let getUserRole (ctx: HttpContext) =
    match ctx.Request.Cookies.TryGetValue("role") with
    | true, role -> 
        let clean = role.Trim().ToLower()
        printfn $"[DEBUG] Süti alapján szerepkör: {clean}"
        clean
    | _ -> 
        printfn "[DEBUG] Süti nincs beállítva, visszaadott szerepkör: guest"
        "guest"

let registerPageHandler : HttpHandler =
    fun next ctx ->
        let lang = getLangFromContext ctx
        let lbls = labels lang
        let role = getUserRole ctx
        htmlView (
            Views.layout role lang lbls [
                div [ _class "login-container" ] [
                    h2 [ _class "login-title" ] [ str lbls.Register ]
                    form [ _action "/register"; _method "post"; _class "login-form" ] [
                        div [] [
                            label [ _for "username" ] [ str lbls.Username ]
                            input [ _type "text"; _name "username"; _id "username" ]
                        ]
                        div [] [
                            label [ _for "password" ] [ str lbls.Password ]
                            input [ _type "password"; _name "password"; _id "password" ]
                        ]
                        div [] [
                            label [ _for "password2" ] [ str lbls.PasswordAgain ]
                            input [ _type "password"; _name "password2"; _id "password2" ]
                        ]
                        input [ _type "submit"; _value lbls.Register; _class "login-btn" ]
                    ]
                ]
            ]
        ) next ctx

let loginHandler : HttpHandler =
    fun next ctx -> task {
        let! form = ctx.Request.ReadFormAsync()
        let username = form.["username"].ToString()
        let password = form.["password"].ToString()
        
        let userFile = Path.Combine(Directory.GetCurrentDirectory(), "users.txt")
        let existingUsers =
            if File.Exists(userFile) then File.ReadAllLines(userFile) else [||]

        match existingUsers |> Array.tryFind (fun line ->
            let parts = line.Split(';')
            parts.Length >= 2 && parts.[0] = username && parts.[1] = password) with

        | Some line ->
            let parts = line.Split(';')
            let role = if parts.Length > 2 then parts.[2].Trim().ToLower() else "guest"

            let options = CookieOptions()
            options.Path <- "/"
            options.HttpOnly <- false
            options.SameSite <- SameSiteMode.Lax
            options.Expires <- Nullable(DateTimeOffset.UtcNow.AddHours(1.0))

            ctx.Response.Cookies.Append("role", role, options)
            logActivity ctx "login" (Some username)
            return! redirectTo false "/" next ctx

        | None ->
            let lang = getLangFromContext ctx
            let lbls = labels lang
            let role = getUserRole ctx
            logActivity ctx "failed_login" (Some username)
            return! htmlView (
                Views.layout role lang lbls [
                    div [] [ str lbls.LoginFailed ]
                ]
            ) next ctx
    }

let loginPageHandler : HttpHandler =
    fun next ctx ->
        let lang = getLangFromContext ctx
        let lbls = labels lang
        let role = getUserRole ctx
        htmlView (
            Views.layout role lang lbls [
                div [ _class "login-container" ] [
                    h2 [ _class "login-title" ] [ str lbls.Login ]
                    form [ _action "/login"; _method "post"; _class "login-form" ] [
                        div [] [
                            label [ _for "username" ] [ str lbls.Username ]
                            input [ _type "text"; _name "username"; _id "username" ]
                        ]
                        div [] [
                            label [ _for "password" ] [ str lbls.Password ]
                            input [ _type "password"; _name "password"; _id "password" ]
                        ]
                        input [ _type "submit"; _value lbls.Login; _class "login-btn" ]
                    ]
                    p [] [
                        a [ _href (sprintf "/register?lang=%s" (match lang with | Hu -> "hu" | En -> "en")) ] [ str lbls.RegisterQ ]
                    ]
                ]
            ]
        ) next ctx

let registerHandler : HttpHandler =
    fun next ctx -> task {
        let lang = getLangFromContext ctx
        let lbls = labels lang
        let! form = ctx.Request.ReadFormAsync()
        let username = form.["username"].ToString()
        let password = form.["password"].ToString()
        let password2 = form.["password2"].ToString()

        let userFile = Path.Combine(Directory.GetCurrentDirectory(), "users.txt")
        let existingUsers =
            if File.Exists(userFile) then File.ReadAllLines(userFile) else [||]

        if existingUsers |> Array.exists (fun line -> line.Split(';')[0] = username) then
            let role = getUserRole ctx
            return! htmlView (Views.layout role lang lbls [ str "❌ Ez a felhasználónév már foglalt." ]) next ctx
        elif password <> password2 then
            let role = getUserRole ctx
            return! htmlView (Views.layout role lang lbls [ str "❌ A jelszavak nem egyeznek." ]) next ctx
        else
            File.AppendAllText(userFile, $"{username};{password};guest\n")
            return! redirectTo false (sprintf "/login?lang=%s" (match lang with | Hu -> "hu" | En -> "en")) next ctx
    }

let logoutHandler : HttpHandler =
    fun next ctx -> task {
        let lang = getLangFromContext ctx
        let lbls = labels lang
        let deleteOptions = CookieOptions()
        deleteOptions.Path <- "/"
        deleteOptions.HttpOnly <- false
        deleteOptions.SameSite <- SameSiteMode.Lax
        deleteOptions.Expires <- Nullable(DateTimeOffset.UtcNow.AddDays(-1.0))

        ctx.Response.Cookies.Delete("role", deleteOptions)
        printfn "[DEBUG] Kijelentkezés, cookie törölve"

        return! htmlView (
            Views.layout "guest" lang lbls [
                script [] [ rawText (sprintf "location.href = '/?lang=%s'" (match lang with | Hu -> "hu" | En -> "en")) ]
            ]
        ) next ctx
    }

let indexHandler : HttpHandler =
    fun next ctx -> task {
        let lang = getLangFromContext ctx
        let lbls = labels lang
        let role = getUserRole ctx
        let msg = { Text = "" }
        let uploads = Path.Combine(Directory.GetCurrentDirectory(), "uploads")
        let files =
            if Directory.Exists uploads then
                Directory.GetFiles uploads
                |> Array.map (fun f ->
                    let name = Path.GetFileName f
                    let date = File.GetCreationTime(f).ToString("yyyy.MM.dd. HH:mm")
                    let dateRaw = File.GetCreationTime(f)
                    let size = FileInfo(f).Length
                    let ext = Path.GetExtension(f)
                    name, date, dateRaw, size, ext)
                |> Array.toList
            else
                []

        let query = ctx.Request.Query
        let sort = if query.ContainsKey("sort") then query.["sort"].ToString() else "name"
        let dir = if query.ContainsKey("dir") then query.["dir"].ToString() else "asc"

        let filesSorted =
            let cmp =
                match sort with
                | "name" -> fun (n,_,_,_,_) (n2,_,_,_,_) -> compare n n2
                | "date" -> fun (_,_,d,_,_) (_,_,d2,_,_) -> compare d d2
                | "size" -> fun (_,_,_,s,_) (_,_,_,s2,_) -> compare s s2
                | "type" -> fun (_,_,_,_,e) (_,_,_,_,e2) -> compare e e2
                | _ -> fun (n,_,_,_,_) (n2,_,_,_,_) -> compare n n2
            let sorted = List.sortWith cmp files
            if dir = "desc" then List.rev sorted else sorted

        let filesForView =
            filesSorted
            |> List.map (fun (name, date, _, size, ext) -> (name, date, size, ext))

        return! htmlView (Views.index msg filesForView role sort dir lbls lang) next ctx
    }

let uploadHandler : HttpHandler =
    fun next ctx -> task {
        let role = getUserRole ctx
        if role <> "admin" then
            return! redirectTo false "/" next ctx
        elif ctx.Request.HasFormContentType then
            let! form = ctx.Request.ReadFormAsync()
            let file = form.Files.["file"]
            let uploads = Path.Combine(Directory.GetCurrentDirectory(), "uploads")
            if not (Directory.Exists uploads) then Directory.CreateDirectory uploads |> ignore
            if file <> null then
                let filePath = Path.Combine(uploads, file.FileName)
                use stream = new FileStream(filePath, FileMode.Create)
                do! file.CopyToAsync(stream)
                logActivity ctx "upload" (Some file.FileName)
            return! redirectTo false "/" next ctx
        else
            return! redirectTo false "/" next ctx
    }

let deleteHandler : HttpHandler =
    fun next ctx -> task {
        let role = getUserRole ctx
        if role <> "admin" then
            return! redirectTo false "/" next ctx
        else
            let! form = ctx.Request.ReadFormAsync()
            let fileName = form.["fileName"].ToString()
            let uploads = Path.Combine(Directory.GetCurrentDirectory(), "uploads")
            let filePath = Path.Combine(uploads, fileName)
            if File.Exists(filePath) then
                File.Delete(filePath)
                logActivity ctx "delete" (Some fileName)
            return! redirectTo false "/" next ctx
    }

let downloadHandler (fileName: string) : HttpHandler =
    fun next ctx -> task {
        let lang = getLangFromContext ctx
        let lbls = labels lang
        let role = getUserRole ctx
        if role = "guest" then
            return! htmlView (
                Views.layout role lang lbls [
                    div [] [ str lbls.DownloadOnlyLoggedIn ]
                    a [ _href (sprintf "/login?lang=%s" (match lang with | Hu -> "hu" | En -> "en")) ] [ str lbls.Login ]
                ]
            ) next ctx
        else
            let uploads = Path.Combine(Directory.GetCurrentDirectory(), "uploads")
            let filePath = Path.Combine(uploads, fileName)
            if File.Exists(filePath) then
                logActivity ctx "download" (Some fileName)
                return! streamFile true filePath None None next ctx
            else
                return! RequestErrors.notFound (text "Fájl nem található.") next ctx
    }

let downloadLogHandler : HttpHandler =
    fun next ctx -> task {
        let logPath = Path.Combine(Directory.GetCurrentDirectory(), "activity.log")
        if File.Exists(logPath) then
            ctx.Response.Headers.Add("Content-Disposition", "attachment; filename=\"activity.log\"")
            return! streamFile true logPath None None next ctx
        else
            return! RequestErrors.notFound (text "Naplófájl nem található.") next ctx
    }

let deleteLogHandler : HttpHandler =
    fun next ctx -> task {
        let logPath = Path.Combine(Directory.GetCurrentDirectory(), "activity.log")
        if File.Exists(logPath) then File.Delete(logPath)
        return! redirectTo false "/" next ctx
    }

let webApp : HttpHandler =
    choose [
        GET >=> choose [
            route "/" >=> indexHandler
            routef "/download/%s" downloadHandler
            route "/login" >=> loginPageHandler
            route "/register" >=> registerPageHandler
            route "/logout" >=> logoutHandler
            route "/download-log" >=> downloadLogHandler
        ]
        POST >=> choose [
            route "/upload" >=> uploadHandler
            route "/delete" >=> deleteHandler
            route "/login" >=> loginHandler
            route "/register" >=> registerHandler
            route "/delete-log" >=> deleteLogHandler
        ]
        setStatusCode 404 >=> text "Not Found"
    ]

let errorHandler (ex: Exception) (logger: ILogger) =
    logger.LogError(ex, "Unhandled exception: %s", ex.Message)
    clearResponse >=> setStatusCode 500 >=> text ex.Message

open Microsoft.Extensions.FileProviders
open Microsoft.AspNetCore.Http

let configureApp (app: IApplicationBuilder) =
    app.UseCookiePolicy()
       .UseStaticFiles()
       .UseStaticFiles(
           StaticFileOptions(
               FileProvider = PhysicalFileProvider(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "uploads")),
               RequestPath = PathString("/uploads")
           )
       )
       .UseGiraffeErrorHandler(errorHandler)
       .UseGiraffe(webApp)

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.CookiePolicy
open Microsoft.Extensions.DependencyInjection

let configureServices (ctx: WebHostBuilderContext) (services: IServiceCollection) =
    services.AddGiraffe() |> ignore

    services.Configure<CookiePolicyOptions>(fun (options: CookiePolicyOptions) ->
        options.MinimumSameSitePolicy <- SameSiteMode.Lax
        options.HttpOnly <- HttpOnlyPolicy.Always
        options.Secure <- CookieSecurePolicy.SameAsRequest
    ) |> ignore

let configureLogging (builder: ILoggingBuilder) =
    builder.AddConsole().AddDebug() |> ignore

[<EntryPoint>]
let main args =
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun webHostBuilder ->
            webHostBuilder
                .UseUrls("http://localhost:5000")
                .Configure(configureApp)
                .ConfigureServices(configureServices)
                .ConfigureLogging(configureLogging)
            |> ignore)
        .Build()
        .Run()
    0
