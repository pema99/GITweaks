# GI Tweaks
This package contains various tools and tweaks for working with global illumination in Unity. Both Unity's builtin Lightmapper and the third-party Bakery Lightmapper are supported, though not all tweaks work with Bakery. [Harmony](https://github.com/pardeike/Harmony) is used for making patches. Tested with Unity 2022.3 on the Builtin Render Pipeline. 

All features can be toggled via the settings window in "Tools > GI Tweaks > Settings".

## How to install

1. Open the package manager (Window > Package Manager).
2. Press the big plus icon, select "Install package from git URL".
3. Paste the URL of the repo `https://github.com/pema99/GITweaks.git` (note the `.git` ending) and click install.

![image](https://github.com/pema99/GITweaks/assets/11212115/133bdd9c-7f87-4714-8b1f-ed5eece77c95)

## Current features

- [Share lightmap space across all LODs](#share-lightmap-space-across-all-lods)
- [Clickable Lightmap Preview charts](#clickable-lightmap-preview-charts)
- [Lightmap index dropdown in Lightmap Preview window](#lightmap-index-dropdown-in-lightmap-preview-window)
- [Optimize lightmap sizes after baking](#optimize-lightmap-sizes-after-baking)
- [Fix lightmap seams between objects](#fix-lightmap-seams-between-objects)
- [Bulk select renderers](#bulk-select-renderers)
- [Baked Transmission view modes](#baked-transmission-view-modes)
- [Better Lighting Data asset inspector](#better-lighting-data-asset-inspector)
- [Show lightmap flags in default material inspector](#show-lightmap-flags-in-default-material-inspector)
- [Automatic embedded Lighting Settings](#automatic-embedded-lighting-settings)
- [Better default Lighting Settings](#better-default-lighting-settings)
- [Convert lightmapped renderer to probe-lit without rebaking](#convert-lightmapped-renderer-to-probe-lit-without-rebaking)
- [New and Clone buttons for Skybox material](#new-and-clone-buttons-for-skybox-material)

### Share lightmap space across all LODs
When using LOD groups, if you use lightmapping for several LOD levels, each LOD level will take up its own space in the lightmap. This tweak adds a script "GI Tweaks Shared LOD" which lets you reuse the same lightmap space for several LOD levels. Simply attach the script to the GameObject that has the LOD group and bake. Unlike some of the other solutions that exist for this, the script will edit the LightingDataAsset stored on disk, meaning you don't need to manually fiddle with lightmap indices, scales and offsets at runtime.

![1zWgISsTpp](https://github.com/pema99/GITweaks/assets/11212115/df0ce872-845d-488e-974a-f158ef57ce3d)

For reference, the same scene baked with the script disabled, and using lightmaps for each LOD level:

![image](https://github.com/pema99/GITweaks/assets/11212115/edcfd2e8-f18c-4166-a3c3-97089e749774)

### Clickable Lightmap Preview charts
The Lightmap Preview window can highlight the UV chart of the currently selected object. However, you cannot inversely click on a chart to select the corresponding object. This tweak adds that functionality.

![ZFvbglRVTT](https://github.com/pema99/GITweaks/assets/11212115/ec36ed87-5bdf-489d-b94d-cbe8c5595bd4)

### Lightmap index dropdown in Lightmap Preview window
This tweak adds a dropdown to the Lightmap Preview window that lets you switch between viewing different lightmaps. A convenient shortcut for opening the window has been added under "Tools > GI Tweaks > Open Lightmap Preview".

Before:

![image](https://github.com/pema99/GITweaks/assets/11212115/7bb1490b-7001-463e-9590-6d3499d24ec2)

After:

![image](https://github.com/pema99/GITweaks/assets/11212115/d3f8f39e-9588-4ea2-a5cf-4fa3100910a8)

### Optimize lightmap sizes after baking
> Note: This feature does not work with the Bakery lightmapper.

> Note: This tweak is _not_ enabled by default, and must be enabled in "Tools > GI Tweaks > Settings".

Unity's builtin Lightmapper has a tendency to produce poorly packed lightmaps in some cases, which leads to wasting VRAM on empty texture space. An example is shown below.

![image](https://github.com/pema99/GITweaks/assets/11212115/dec8c317-2360-437a-b76d-e8bbbfae7f0a)

When a bake is finished with this tweak enabled, the lightmap packing will be re-done, producing a new set of lightmaps, each of which is packed more tightly. These new lightmaps will often be smaller than the original lightmaps, and may be different sizes. Instances in each lightmap will never be resized, so there should be any noticeable quality difference. Below is the result of using the feature on the lightmap shown above. Before optimization, the scene had a single 512x512 lightmap. After optimization, the scene uses two 256x256 lightmaps - a 2x reduction in VRAM usage:

![image](https://github.com/pema99/GITweaks/assets/11212115/157cb1c6-4fac-4d9c-9538-35bd19761ce6)

The tweak is configurable via two additional settings:
- **Target coverage %** is a threshold determining when lightmap size optimization will be enabled. If less than the specified percentage of lightmaps texels are covered, optimization will be done. This should usually be set pretty high.
- **Minimum Lightmap Size** determines the minimum allowed lightmap size after optimization. If you want to avoid many small lightmaps, increase this value. If you set it too high, no optimization will be done.

![image](https://github.com/pema99/GITweaks/assets/11212115/9e73b53d-d806-4340-a2ca-0d86fc2cfd66)

### Fix lightmap seams between objects
When baking scenes containing surfaces built of multiple modular pieces, you will often get seams where the pieces meet, due to differences in bilinear sampling. Unity has a solution for [fixing seams](https://docs.unity3d.com/Manual/Lightmapping-SeamStitching.html) on a single renderer, but nothing to fix seams between different renderers, which is typically worked around by manually merging meshes. This tweak provides some tools for mitigating seams between different renderers.

It can be used in 2 primary ways: As a volume, and as a targeted component. The gif below shows an example of using the "GI Tweaks Seam Fix Volume" variant to fix a seam. Volume components can be quickly created with the right click context menu / GameObject menu.

![s2o3BE4np4](https://github.com/pema99/GITweaks/assets/11212115/c0a9e89e-2693-40ae-a0d5-4284f0358e8a)

![image](https://github.com/pema99/GITweaks/assets/11212115/73b7bce1-09d5-40ce-a6f2-5797b0c5d79f)

The volume component will only attempt to fix seams occuring within the volume. Alternatively, you can use the targeted "GI Tweaks Seam Fix" component, shown below. This component is applied directly onto the object exhibiting seams, and should be provided a list of other renderers contributing to the seams. In this example, the component is applied to a cube, and pointed to another cube, in effect fixing the seam between the 2 cubes.

![l42h87mouz](https://github.com/pema99/GITweaks/assets/11212115/440f165f-2256-44b4-bd8a-08afc3d7cc17)

The tweak is configurable via some additional settings:
- **Run After Baking** controls whether the seam fix should be applied automatically when baking.
- **Max Surface Angle** is the maximum allowed angle in degrees between 2 surfaces for them to be considered "the same". This is used to prevent fixing intentional seams, such as the corners of a cube.
- **Seam Fix Strength** controls how aggresively the seam fixing algorithm blurs the seam.
- **Max Solver Iteration Count** controls how many iterations the algorithm takes at maximum. Higher numbers may give better quality, but will be slower.
- **Solver Tolerance** is an error threshold which, when reached, will cause the seam fixing algorithm to stop early.
- (Volume variant only) **Renderers To Exclude** is a list of renderers to ignore, even if they overlap the volume.
- (Targeted variant only) **Renderers To Fix Seams With** is a list of renderers to run the seam fixing algorithm for.

The "Preview fix" and "Reset preview" button can be used to non-destructively preview the result of applying the seam fix. "Apply fix" will permanently modify the lightmap texture on disk.

> Note: This tweak **only** fixes hard seams due to differences in bilinear filtering. If the lightmaps have perceptually different colors at the seam, this tweak will not fix that.

> Note: Seam fixing can be expensive, especially with the volume component. Try not to make huge volumes - instead, keep the small and use only where seams occur.

### Bulk select renderers
Making bulk lighting-related changes to renderers in a large scene is tedious. This tool provides a simple way to mass-select renderers based on some configurable filters, for the purpose of multi-editing them. Accesible via "Tools > GI Tweaks > Bulk Renderer Selection".

![image](https://github.com/pema99/GITweaks/assets/11212115/95754281-4f98-4d1a-a480-542b3a2f7523)

### Baked Transmission view modes
The rules for what is considered transmissive/transparent by the builtin lightmapper are somewhat opaque. These added scene view modes allow for easy identification and debugging of transparents. There are 2 modes, both accessible from the scene view toolbar:

![image](https://github.com/pema99/GITweaks/assets/11212115/ddd63e87-da58-4183-a756-ef1b47aab180)

"Baked Transmission Modes" displays what the baker sees as transmissive:

![Unity_c35PO3McfI](https://github.com/pema99/GITweaks/assets/11212115/5e7eed73-ac73-4a8a-907e-1d65b4d8ae8a)

"Baked Transmission Data" displays the actual transmission textures fed to the baker:

![uwJ51JYeAA](https://github.com/pema99/GITweaks/assets/11212115/783bedb2-0e4e-46dd-a9b0-826f8c2b6e62)

### Better Lighting Data asset inspector
The output of the a bake - the Lighting Data asset - is a black box. The asset's inspector doesn't show any information about the contents. This tweak changes the default inspector to display all the contained data. Warning: Modifying this data can screw up your bake. Any issues should be resolved by simply rebaking, though.

Before:

![image](https://github.com/pema99/GITweaks/assets/11212115/b8d52401-bfb7-4e46-bbbe-e0b1677b8da7)

After:

![image](https://github.com/pema99/GITweaks/assets/11212115/24644bdc-78a5-4b4f-837a-95d13508b562)

### Show lightmap flags in default material inspector
Unity has a hidden `MaterialGlobalIlluminationFlags` on each material, which must be set in order for baked emission to work. It is in't shown in the inspector by default. This tweak shows it.

Before:

![zDSlKh9F6x](https://github.com/pema99/GITweaks/assets/11212115/7506060e-6132-46d8-9af8-add9fc2aca3c)

After:

![Om0RfPY6M5](https://github.com/pema99/GITweaks/assets/11212115/e61383fc-b1ea-493f-af58-1be6884d16b6)

### Automatic embedded Lighting Settings
When you create a new scene in Unity, it will by default have no Lighting Settings asset assigned, and you won't be able to edit any settings without creating one. This tweak instead assigns a new embedded Lighting Settings asset which is serialized directly into the scene, letting you immediately modify settings without having to create an additional asset.

Before:

![fJ3RSEK0VC](https://github.com/pema99/GITweaks/assets/11212115/3fe8e37d-1826-4b93-b67c-4b8702df0c41)

After:

![N9cgB5O7BE](https://github.com/pema99/GITweaks/assets/11212115/bd2e34ba-4b42-4d61-920d-512e4a0dcc5b)

### Better default Lighting Settings
When you create a new scene or Lighting Settings asset, the CPU lightmapper is the default choice of baking backend. The CPU lightmap is very slow and shouldn't be used if you have a GPU. This tweak changes the default to be the GPU lightmapper. Additionally, it disables the "Progressive Updates" checkbox by default, which can slow down bakes heavily.

Before:

![D909MxI0nx](https://github.com/pema99/GITweaks/assets/11212115/5cab3c26-48a7-4173-b836-8609a02f47a4)

After:

![Unity_Vdixu05Zlp](https://github.com/pema99/GITweaks/assets/11212115/713c5598-3857-4e33-8c60-160cc35ceded)

### Convert lightmapped renderer to probe-lit without rebaking
When you have a lightmapped renderer, and you want to change it to be lit by probes, changing the setting in the inspector won't immediately make the change. For the setting to apply, you must bake again! This tweak adds a button to the inspector that only appears when you have changed a previously lightmapped renderer to be probe-lit, and allows you to immediately apply the change to the Lighting Data asset without having to re-bake.

![6C1yre8Mth](https://github.com/pema99/GITweaks/assets/11212115/1cceef7a-e976-4283-b9db-a5ade9cd09cb)

### New and Clone buttons for Skybox material
This tweak adds some buttons for quickly creating new Skybox Materials and assigning them in the Environment tab of the Lighting Window.

![image](https://github.com/pema99/GITweaks/assets/11212115/43c6cc79-96d1-4302-b310-de252b08d6c5)


