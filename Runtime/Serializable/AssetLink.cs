using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace XSystem
{
    public interface IAddressLink<T>
    {
        AsyncOperationHandle<T> LoadAssetAsync();
        T Asset { get; }
        AsyncOperationHandle OperationHandle { get; }
        bool IsValid();
        bool IsDone { get; }
        void ReleaseAsset();
    }

    [System.Serializable]
    public class AssetLink<T> : AssetReferenceT<T>, IAddressLink<T> where T : Object
    {
        public AssetLink() : base(string.Empty) { }

        public AssetLink(string guid) : base(guid) { }

        public new T Asset => base.Asset as T;

        T IAddressLink<T>.Asset => Asset;
    }
}
