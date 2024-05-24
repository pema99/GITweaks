# Slang Unity Plugin

This is a plugin to use [Slang](https://github.com/shader-slang/slang) code from within Unity ShaderLab shaders. It adds 2 new asset types, `.slangshader` and `.slang`. The `.slangshader` asset type is like a regular Unity shader, but where you can write Slang code in `CGPROGRAM` and `CGINCLUDE` blocks. The `.slang` asset type is just a shader include file.

For more information about Slang, check out [their website](https://shader-slang.com/slang/).

# Installation

Either clone the repo and add it as a local package, or add the package directly from the Unity package manager using the git link `https://github.com/pema99/SlangUnityPlugin.git`:

![image](https://github.com/pema99/SlangUnityPlugin/assets/11212115/4fb0045e-a1f3-4f46-8c56-9ddc2ed4ee46)

# Caveats

This plugin was mostly made for me to play around with the Slang language inside of Unity, and at some point out of stubbornness. Adding support for a new shading language entirely from within user code, isn't exactly a supported usecase, and for this reason, the plugin makes use of several undocumented features and engine hacks/tricks. It should "just work" for most use cases, but there are some noteworthy unsolved caveats:

- In order to track which Shader variants are needed, the plugin hijacks API backing the shader variant collection warmup feature, exposed in project settings (Project Settings > Graphics > Shader Loading > Save to asset...). The plugin may interfere with normal usage of this feature when Slang-based shaders are edited. If you intend to use the feature, don't edit any Slang-based shaders while you use it.
- A folder will be created in Assets/SlangShaderCache. This folder can be safely deleted, but will regenerate itself whenever a Slang-based shader is imported. This is again needed to track variants.
- The plugin uses [Harmony](https://github.com/pardeike/Harmony) to patch some internal methods that are used for building asset bundles and the player. When a build is started, the plugin will traverse each scene to figure out which shader variants should be included into the build. If you are already patching these methods, the plugin might malfunction.
- Currently, whenever additional shader variants are requested for compilation, all variants for the given will be recompiled, instead of just the additionally requested ones. This makes shader compilation a bit slower than it principially has to be.
- Whenever a Slang-based shader is deleted or renamed, other Slang-based shaders in the project may be recompiled. This is a limitation of the shader variant collection warmup feature I am hijacking.
