# Mailr

`Mailr` is an ASP.NET-Core service for sending emails.

## Features

- Versioning - it supports URL versioning, e.g. `http://{{mailr}}/api/v1.0/mailr/messages/test`.
- Design mode - in this mode emails are only rendered but not sent. You activate it with `isDesignMode=true` query-string parameter.
- Exensibility - by default `Mailr` provides only a test email and a plaintext one. More views can be added via extensions.

## Installing extensions

You install extensions by putting them in `ext` directory that has the following structure:

```
/ext
    /Mailr.Extensions.<YourExtension>
        /bin
        /src
            /Views
        /wwwroot
```

- `bin` - the binaries go here
- `Views` - this where you put views
- `wwwroot` - this contains other files like `*.js` or `*.css`

## Developing extensions

To create extensions you need to 

- checkout these two repositories
    - [mailr](https://github.com/he-dev/mailr)
    - [mailr-extensions-native](https://github.com/he-dev/mailr-extensions-native)
- create a new repository for your own extensions and put it in the same directory as the other two, e.g.
    ```
    \repos
        \mailr
        \mailr-extensions-native
        \mailr-extensions-other
    ```
- open the `mailr.sln` 
- add your extensions' projects to the `ext` solution folder
- reference `lib\Mailr.Extensions` in your new project
- **do not** add its reference to the `Mailr` project because extensions are loaded dynamically at runtime. Instead select the `DevelopmentExt` configuration.
- adjust the `appsettings.DevelopmentExt.json` and add paths to your extensions there (`SolutionDirectory` currently supports only absolute paths)
- check solution configuration and select the checkmark next to your extension so that it's also compiled
- to build the solution use any configuration but the ones with the `*.csproj` suffix which are ment for `Mailr` development only and require the [reusable](https://github.com/he-dev/reusable) repository to be checked out

## Publishing extensions

Build `Mailr` in `Release` mode and publish it. Copy all files to their target directories. Currently there are no `msbuild` settings for it.

