# GI Tweaks
**This is a work in progress, and may contain bugs.**

This package contains various tools and tweaks for working with global illumination in Unity. Tested with Unity 2022.3 on the Builtin Render Pipeline. [Harmony](https://github.com/pardeike/Harmony) is used for making patches.

## Current features

### Mass select renderers
Making bulk lighting-related changes to renderers in a large scene is tedious. This tool provides a simple way to mass-select renderers based on some configurable filters, for the purpose of multi-editing them.

![image](https://github.com/pema99/GITweaks/assets/11212115/95754281-4f98-4d1a-a480-542b3a2f7523)

### Share lightmap space across all LODs
When using LOD groups, if you use lightmapping for several LOD levels, each LOD level will take up its own space in the lightmap. This tweak adds a script "GI Tweaks Shared LOD" which lets you reuse the same lightmap space for several LOD levels. Simply attach the script to the GameObject that has the LOD group and bake. Unlike some of the other solutions that exist for this, the script will edit the LightingDataAsset stored on disk, meaning you don't need to manually fiddle with lightmap indices, scales and offsets at runtime.

![1zWgISsTpp](https://github.com/pema99/GITweaks/assets/11212115/df0ce872-845d-488e-974a-f158ef57ce3d)

For reference, the same scene baked with the script disabled, and using lightmaps for each LOD level:

![image](https://github.com/pema99/GITweaks/assets/11212115/edcfd2e8-f18c-4166-a3c3-97089e749774)

### Clickable Lightmap Preview charts
The Lightmap Preview window can highlight the UV chart of the currently selectec object. However, you cannot inversely click on a chart to select the corresponding object. This tweak adds that functionality.

![ZFvbglRVTT](https://github.com/pema99/GITweaks/assets/11212115/ec36ed87-5bdf-489d-b94d-cbe8c5595bd4)

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

