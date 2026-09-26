# XSystem Serialization

Unity 프로젝트에서 재사용할 수 있는 직렬화 컨테이너, Addressables 기반 에셋 링크, Inspector 특성(Attribute)을 제공합니다.

공개 API는 `XSystem` 네임스페이스에 있습니다.

`[ShowInInspector]`를 사용하면 Unity Inspector에 프로퍼티나 직렬화되지 않은 private 필드를 표시할 수 있습니다. setter가 없는 프로퍼티는 읽기 전용이며, 지원되는 기본형, enum, vector, color, Unity 오브젝트 값은 편집할 수 있습니다.

```csharp
[ShowInInspector]
private int _runtimeValue;

[ShowInInspector]
public float CurrentSpeed => _currentSpeed;
```

## 제공 기능

`xsystem.serialization`에는 XSystem Framework의 다른 패키지 없이도 사용할 수 있는 Unity용 직렬화 유틸리티가 포함되어 있습니다.

* `SerializedDictionary<TKey, TValue>`는 키와 값을 별도의 목록으로 저장하며, Inspector drawer를 통해 항목을 편집할 수 있습니다.
* `AssetLink<T>`는 Unity Addressables의 `AssetReferenceT<T>`를 확장하고, 링크를 사용하는 코드에서 타입이 지정된 에셋에 접근할 수 있는 `IAddressLink<T>` 인터페이스를 구현합니다.
* `PageAttribute`와 `SearchableAttribute`를 사용하면 Unity Inspector에서 지원되는 컬렉션 필드에 페이지 나누기와 텍스트 필터링을 적용할 수 있습니다.

필요한 커스텀 Property Drawer가 패키지에 포함되어 있으므로, 설치 후 해당 타입과 특성을 Unity Inspector에서 바로 사용할 수 있습니다.

## Inspector 특성

* `[ShowInInspector]`는 직렬화되지 않은 필드와 프로퍼티를 Unity Inspector에 표시합니다. setter가 없는 프로퍼티는 읽기 전용으로 표시됩니다.
* `[Page]`는 지원되는 컬렉션 필드의 항목을 페이지로 나눕니다. `[Page(20)]`처럼 항목 수를 지정할 수 있으며, 기본값은 페이지당 10개입니다.
* `[Searchable]`은 지원되는 컬렉션 필드에 텍스트 검색 필터를 표시합니다. `[Page]`와 함께 사용하면 검색 결과에도 페이지 나누기가 적용됩니다.
* `[AssetLinkHeight(48f)]`는 Unity Inspector에서 해당 `AssetLink<T>` 필드의 표시 높이를 지정합니다. 지정하지 않으면 기본 높이를 사용합니다.

```csharp
using System.Collections.Generic;
using UnityEngine;
using XSystem;

[SerializeField, Page(20), Searchable]
private List<string> _items;

[SerializeField, AssetLinkHeight(48f)]
private AssetLink<Texture2D> _thumbnail;
```

## Git에서 설치

`[xsystem-unity-serialization](https://github.com/josangjun/xsystem-unity-serialization)` 저장소에서 패키지를 설치합니다.

### Package Manager

1. Unity에서 `Window > Package Manager`를 엽니다.
2. `+`를 클릭하고 `Add package from git URL...`을 선택합니다.
3. 다음 URL을 입력합니다.

```text
https://github.com/josangjun/xsystem-unity-serialization.git
```

### manifest.json에서 설치

또는 `Packages/manifest.json`에 다음 의존성을 추가합니다.

```json
{
  "dependencies": {
    "xsystem.serialization": "https://github.com/josangjun/xsystem-unity-serialization.git"
  }
}
```

특정 브랜치, 태그 또는 커밋을 설치하려면 URL 끝에 `#<ref>`를 추가합니다.

```text
https://github.com/josangjun/xsystem-unity-serialization.git#v1.0.0
```
