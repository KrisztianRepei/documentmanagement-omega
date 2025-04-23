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

module Views =
    open Giraffe.ViewEngine
    open Giraffe.ViewEngine.HtmlElements

    let layout (role: string) (content: XmlNode list) : XmlNode =
        let authLinks =
            if role = "guest" || role = "" then
                [ a [ _href "/login"; _class "login-btn" ] [ str "🔑 Bejelentkezés" ] ]
            else
                [ a [ _href "/logout"; _class "logout-btn" ] [ str "🚪 Kijelentkezés" ] ]

        let headerContent =
            [
                div [ _class "header" ] [
                    h1 [] [ str "📁 Dokumentumkezelő rendszer" ]
                    div [ _class "auth-link" ] (
                        [
                            button [
                                _id "darkModeToggle"
                                _type "button"
                                _class "darkmode-toggle"
                            ] [ str "🌗" ]
                        ] @ authLinks
                    )
                ]
            ]

        html [] [
            head [] [
                title [] [ str "Dokumentumkezelő" ]
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
                        """
                    ]
                ]
            )
        ]
    let uploadForm : XmlNode =
        form [
            _action "/upload"
            _method "post"
            attr "enctype" "multipart/form-data"
        ] [
            p [] [ input [ _type "file"; _name "file" ] ]
            p [] [
                input [
                    _type "submit"
                    _value "⬆️ Feltöltés"
                    _class "upload-btn"
                ]
            ]
        ]

    let getFileIcon (fileName: string) : string =
        match System.IO.Path.GetExtension(fileName).ToLower() with
        | ".pdf" -> "📕"
        | ".doc" | ".docx" -> "📄"
        | ".png" | ".jpg" | ".jpeg" -> "🖼️"
        | ".zip" | ".rar" -> "📦"
        | _ -> "📁"

    let fileList (files: (string * string) list) (role: string) : XmlNode =
        ul [] (
            files
            |> List.map (fun (file, createdAt) ->
                let downloadButton =
                    if role <> "guest" then
                        [ a [ _href $"/download/{file}"; _class "download-btn" ] [ str "📥 Letöltés" ] ]
                    else []

                let deleteForm =
                    if role = "admin" then
                        [ form [
                            _action "/delete"
                            _method "post"
                            attr "onsubmit" "return confirm('Biztosan törölni szeretnéd?');"
                            _style "display:inline;"
                        ] [
                            input [ _type "hidden"; _name "fileName"; _value file ]
                            input [ _type "submit"; _value "🗑️ Törlés"; _class "delete-btn" ]
                        ] ]
                    else []

                li [] [
                    div [] [ str $"{getFileIcon file} {file} 🕒 {createdAt}" ]
                    div [] (downloadButton @ deleteForm)
                ])
        )

    let index (model: Message, files: (string * string) list, role: string) : XmlNode =
        let adminSection =
            if role = "admin" then
                div [ _class "left" ] [
                    h2 [] [ str "Feltöltés" ]
                    p [] [ str model.Text ]
                    uploadForm
                ]
            else
                emptyText

        layout role [
            div [ _class "container" ] [
                adminSection
                div [ _class "right" ] [
                    h2 [] [ str "📂 Feltöltött fájlok" ]
                    fileList files role
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

let loginPageHandler : HttpHandler =
    fun next ctx ->
        let role = getUserRole ctx
        printfn $"[DEBUG] indexHandler szerepkör: {role}"
        htmlView (
            Views.layout role [
                div [ _class "login-container" ] [
                    h2 [ _class "login-title" ] [ str "Bejelentkezés" ]
                    form [ _action "/login"; _method "post"; _class "login-form" ] [
                        div [] [
                            label [ _for "username" ] [ str "Felhasználónév:" ]
                            input [ _type "text"; _name "username"; _id "username" ]
                        ]
                        div [] [
                            label [ _for "password" ] [ str "Jelszó:" ]
                            input [ _type "password"; _name "password"; _id "password" ]
                        ]
                        input [ _type "submit"; _value "Belépés"; _class "login-btn" ]
                    ]
                    p [] [
                        a [ _href "/register" ] [ str "Nincs fiókod? Regisztrálj itt!" ]
                    ]
                ]
            ]
        ) next ctx

let registerPageHandler : HttpHandler =
    fun next ctx ->
        let role = getUserRole ctx
        htmlView (
            Views.layout role [
                div [ _class "login-container" ] [
                    h2 [ _class "login-title" ] [ str "Regisztráció" ]
                    form [ _action "/register"; _method "post"; _class "login-form" ] [
                        div [] [
                            label [ _for "username" ] [ str "Felhasználónév:" ]
                            input [ _type "text"; _name "username"; _id "username" ]
                        ]
                        div [] [
                            label [ _for "password" ] [ str "Jelszó:" ]
                            input [ _type "password"; _name "password"; _id "password" ]
                        ]
                        div [] [
                            label [ _for "password2" ] [ str "Jelszó ismét:" ]
                            input [ _type "password"; _name "password2"; _id "password2" ]
                        ]
                        input [ _type "submit"; _value "Regisztráció"; _class "login-btn" ]
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
            parts.[0] = username && parts.[1] = password) with

        | Some line ->
            let parts = line.Split(';')
            let role = parts.[2].Trim().ToLower()

            let options = CookieOptions()
            options.Path <- "/"
            options.HttpOnly <- false
            options.SameSite <- SameSiteMode.Lax
            options.Expires <- Nullable(DateTimeOffset.UtcNow.AddHours(1.0))

            ctx.Response.Cookies.Append("role", role, options)
            printfn $"[DEBUG] Cookie beállítva: role = {role}"

            return! redirectTo false "/" next ctx

        | None ->
            let role = getUserRole ctx
            return! htmlView (
                Views.layout role [
                    div [] [ str "Sikertelen bejelentkezés" ]
                ]
            ) next ctx
    }


let registerHandler : HttpHandler =
    fun next ctx -> task {
        let! form = ctx.Request.ReadFormAsync()
        let username = form.["username"].ToString()
        let password = form.["password"].ToString()
        let password2 = form.["password2"].ToString()

        let userFile = Path.Combine(Directory.GetCurrentDirectory(), "users.txt")
        let existingUsers =
            if File.Exists(userFile) then File.ReadAllLines(userFile) else [||]

        if existingUsers |> Array.exists (fun line -> line.Split(';')[0] = username) then
            let role = getUserRole ctx
            return! htmlView (Views.layout role [ str "❌ Ez a felhasználónév már foglalt." ]) next ctx
        elif password <> password2 then
            let role = getUserRole ctx
            return! htmlView (Views.layout role [ str "❌ A jelszavak nem egyeznek." ]) next ctx
        else
            File.AppendAllText(userFile, $"{username};{password};guest\n")
            return! redirectTo false "/login" next ctx
    }

let logoutHandler : HttpHandler =
    fun next ctx -> task {
        // A cookie törléséhez opciók
        let deleteOptions = CookieOptions()
        deleteOptions.Path <- "/"
        deleteOptions.HttpOnly <- false
        deleteOptions.SameSite <- SameSiteMode.Lax
        deleteOptions.Expires <- Nullable(DateTimeOffset.UtcNow.AddDays(-1.0))

        // Cookie törlése
        ctx.Response.Cookies.Delete("role", deleteOptions)
        printfn "[DEBUG] Kijelentkezés, cookie törölve"

        // Oldal újratöltés JS segítségével, hogy biztosan frissüljön a nézet
        return! htmlView (
            Views.layout "guest" [
                script [] [ rawText "location.href = '/'" ]
            ]
        ) next ctx
    }

let indexHandler : HttpHandler =
    fun next ctx -> task {
        // Szerepkör meghatározása cookie alapján
        let role = getUserRole ctx
        let msg = { Text = "" }

        // Fájlok listázása
        let uploads = Path.Combine(Directory.GetCurrentDirectory(), "uploads")
        let files =
            if Directory.Exists uploads then
                Directory.GetFiles uploads
                |> Array.map (fun f ->
                    let name = Path.GetFileName f
                    let date = File.GetCreationTime(f).ToString("yyyy.MM.dd. HH:mm")
                    name, date)
                |> Array.toList
            else []

        // Nézet visszaadása a megfelelő szerepkörrel
        return! htmlView (Views.index (msg, files, role)) next ctx
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
            if File.Exists(filePath) then File.Delete(filePath)
            return! redirectTo false "/" next ctx
    }

let downloadHandler (fileName: string) : HttpHandler =
    fun next ctx -> task {
        let role = getUserRole ctx
        if role = "guest" then
            return! htmlView (
                Views.layout role [
                    div [] [ str "🔒 Csak bejelentkezett felhasználók tölthetnek le fájlokat." ]
                    a [ _href "/login" ] [ str "Bejelentkezés" ]
                ]
            ) next ctx
        else
            let uploads = Path.Combine(Directory.GetCurrentDirectory(), "uploads")
            let filePath = Path.Combine(uploads, fileName)
            if File.Exists(filePath) then
                return! streamFile true filePath None None next ctx
            else
                return! RequestErrors.notFound (text "Fájl nem található.") next ctx
    }

let webApp : HttpHandler =
    choose [
        GET >=> choose [
            route "/" >=> indexHandler
            routef "/download/%s" downloadHandler
            route "/login" >=> loginPageHandler
            route "/register" >=> registerPageHandler
            route "/logout" >=> logoutHandler // <- fontos, hogy itt legyen!
        ]
        POST >=> choose [
            route "/upload" >=> uploadHandler
            route "/delete" >=> deleteHandler
            route "/login" >=> loginHandler
            route "/register" >=> registerHandler
        ]
        setStatusCode 404 >=> text "Not Found"
    ]

let errorHandler (ex: Exception) (logger: ILogger) =
    logger.LogError(ex, "Unhandled exception: %s", ex.Message)
    clearResponse >=> setStatusCode 500 >=> text ex.Message

let configureApp (app: IApplicationBuilder) =
    app.UseCookiePolicy()
       .UseStaticFiles()
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