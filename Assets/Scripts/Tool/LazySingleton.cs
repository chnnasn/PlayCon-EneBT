using UnityEngine;

namespace Tools
{
    public abstract class LazySingleton<T> : MonoBehaviour where T : LazySingleton<T>
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _isShuttingDown;

        public static T MainInstance
        {
            get
            {
                if (!Application.isPlaying)
                    return _instance;

                if (_isShuttingDown)
                    return null;

                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_isShuttingDown)
                            return null;

                        _instance = FindObjectOfType<T>() as T;

                        if (_instance == null)
                        {
                            GameObject go = new GameObject(typeof(T).Name);
                            _instance = go.AddComponent<T>();
                        }
                    }
                }

                return _instance;
            }
        }

        public static bool TryGetExistingInstance(out T instance)
        {
            instance = _instance;
            if (instance != null)
                return true;

            if (!Application.isPlaying)
                return false;

            if (_isShuttingDown)
                return false;

            instance = FindObjectOfType<T>() as T;
            _instance = instance;
            return instance != null;
        }

        protected  virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = (T)this;
                _isShuttingDown = false;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnApplicationQuit()
        {
            _isShuttingDown = true;
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

    }
    
}