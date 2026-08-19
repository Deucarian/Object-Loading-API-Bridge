# Third-party notices

This notice describes the dependency and distribution inventory for `com.deucarian.object-loading.api-integration` `0.2.5`. It does not replace the repository's [MIT license](LICENSE.md), and it does not grant rights to software supplied separately.

## Review basis

The reviewed baseline is `origin/main` commit `90fdb6ebeb4e56ee8efbf5467f8f3bb5f91895c5`. Its `npm pack --dry-run` inventory contained 44 package files. The tracked and packed inventories were checked for common vendor/third-party directories, compiled binaries and archives, Git submodules, Git LFS pointers, separate license markers, and media/font assets.

That inventory identified no files marked or located as vendored third-party source, no compiled binary/archive candidates, no submodules, no LFS pointers, and no media/font asset candidates. The dependencies below are resolved separately by Unity Package Manager; they are not copied into this repository's package archive.

## Deucarian dependencies (not third-party)

| Package | Version | Relationship | License |
|---|---:|---|---|
| `com.deucarian.api` | `1.1.3` | Direct target-package dependency | [MIT](https://github.com/Deucarian/API/blob/main/LICENSE.md) |
| `com.deucarian.object-loading` | `1.2.1` | Direct target-package dependency | [MIT](https://github.com/Deucarian/Object-Loading/blob/main/LICENSE.md) |

## External package dependencies

| Package | Version | Provider / purpose | Applicable terms |
|---|---:|---|---|
| `com.unity.nuget.newtonsoft-json` | `3.2.2` | Unity package wrapping Newtonsoft.Json `13.0.2` | [Unity package license](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/license/LICENSE.html); [embedded MIT components](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/license/Third%20Party%20Notices.html) |

The Newtonsoft package's official third-party notice identifies Newtonsoft.Json, Json.Net.Unity3D, Newtonsoft.Json-for-Unity, and com.newtonsoft.json as MIT-licensed components. Their license text travels with the separately resolved Unity package rather than this repository.

## Host platform

The manifest requires Unity `2021.3`. Unity is not included in this package and is governed by the applicable [Unity Editor Software Terms](https://unity.com/legal/editor-terms-of-service/software).

Re-run the inventory and update this notice whenever dependencies or distributed content change.
