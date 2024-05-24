# GI Tweaks
**This is a work in progress, and may contain bugs.**

This package contains various tools and tweaks for working with global illumination in Unity. Tested with Unity 2022.3 on the Builtin Render Pipeline. [Harmony](https://github.com/pardeike/Harmony) is used for making patches.

## Current features

### Mass select renderers
Making bulk lighting-related changes to renderers in a large scene is tedious. This tool provides a simple way to mass-select renderers based on some configurable filters, for the purpose of multi-editing them.

![image](https://github.com/pema99/GITweaks/assets/11212115/95754281-4f98-4d1a-a480-542b3a2f7523)

### Better Lighting Data asset inspector
The output of the a bake - the Lighting Data asset - is a black box. The asset's inspector doesn't show any information about the contents. This tweak changes the default inspector to display all the contained data. Warning: Modifying this data can screw up your bake. Any issues should be resolved by simply rebaking, though.

![image](https://github.com/pema99/GITweaks/assets/11212115/24644bdc-78a5-4b4f-837a-95d13508b562)

### Share lightmap space across all LODs
When using LOD groups, if you use lightmapping for several LOD levels, each LOD level will take up its own space in the lightmap. This tweak adds a script "GI Tweaks Shared LOD" which lets you reuse the same lightmap space for several LOD levels. Simply attach the script to the GameObject that has the LOD group and bake. Unlike some of the other solutions that exist for this, the script will edit the LightingDataAsset stored on disk, meaning you don't need to manually fiddle with lightmap indices, scales and offsets at runtime.

![1zWgISsTpp](https://github.com/pema99/GITweaks/assets/11212115/df0ce872-845d-488e-974a-f158ef57ce3d)

For reference, the same scene baked with the script disabled, and using lightmaps for each LOD level:

![image](https://github.com/pema99/GITweaks/assets/11212115/edcfd2e8-f18c-4166-a3c3-97089e749774)

### Show lightmap flags in default material inspector
Unity has a hidden `MaterialGlobalIlluminationFlags` on each material, which must be set in order for baked emission to work. It is in't shown in the inspector by default. This tweak shows it.

![image](https://github.com/pema99/GITweaks/assets/11212115/bfdd1ef7-5dfe-4c81-84bf-891b06583f06)

### Automatic embedded Lighting Settings
When you create a new scene in Unity, it will by default have no Lighting Settings asset assigned, and you won't be able to edit any settings without creating one. This tweak instead assigns a new embedded Lighting Settings asset which is serialized directly into the scene, letting you immediately modify settings without having to create an additional asset.

Before:

![fJ3RSEK0VC](https://github.com/pema99/GITweaks/assets/11212115/3fe8e37d-1826-4b93-b67c-4b8702df0c41)

After:

![N9cgB5O7BE](https://github.com/pema99/GITweaks/assets/11212115/bd2e34ba-4b42-4d61-920d-512e4a0dcc5b)

### Better default Lighting Settings
When you create a new scene or Lighting Settings asset, the CPU lightmapper is the default choice of baking backend. The CPU lightmap is very slow and shouldn't be used if you have a GPU. This tweak changes the default to be the GPU lightmapper. Additionally, it disables the "Progressive Updates" checkbox by default, which can slow down bakes heavily.

![image](https://github.com/pema99/GITweaks/assets/11212115/ae230e33-39be-4bef-bedc-6c61bedc50b8)
