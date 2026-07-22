# XSystem Serialization

Reusable serialized containers, soft asset links, and inspector attributes for Unity projects.

The public API remains in the `XSystem` namespace.

`[ShowInInspector]` displays a property or a non-serialized private field in the Unity Inspector. Properties without a setter are read-only; supported primitive, enum, vector, color, and Unity object values can be edited.

```csharp
[ShowInInspector]
private int _runtimeValue;

[ShowInInspector]
public float CurrentSpeed => _currentSpeed;
```

## What It Provides

`xsystem.serialization` contains Unity-focused serialization utilities that are useful without the rest of XSystem Framework.

* `SerializedDictionary<TKey, TValue>` serializes dictionary data through parallel key and value lists, with an inspector drawer for editing entries.
* `SoftLink<T>` and `AssetLink<T>` store asset references as GUID-based links and load them through Addressables.
* `PageAttribute` and `SearchableAttribute` add pagination and text filtering to supported collection fields in the Unity Inspector.

The package includes the required custom property drawers, so these types and attributes work directly in the Unity Inspector after installation.

## Install From Git

Install the package from the [xsystem-unity-serialization](https://github.com/josangjun/xsystem-unity-serialization) repository.

### Package Manager

1. Open `Window > Package Manager` in Unity.
2. Click `+` and select `Add package from git URL...`.
3. Enter the following URL.

```text
https://github.com/josangjun/xsystem-unity-serialization.git
```

### manifest.json

Alternatively, add the dependency to `Packages/manifest.json`.

```json
{
  "dependencies": {
    "xsystem.serialization": "https://github.com/josangjun/xsystem-unity-serialization.git"
  }
}
```

To install a specific branch, tag, or commit, append `#<ref>` to the URL.

```text
https://github.com/josangjun/xsystem-unity-serialization.git#v1.0.0
```
