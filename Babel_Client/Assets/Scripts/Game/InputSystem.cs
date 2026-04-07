using System;
using QFramework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Babel
{
	/// <summary>
	/// 表示一次点击/蓄力输入的统一上下文数据。
	/// </summary>
	public readonly struct PointerInputContext
	{
		/// <summary>
		/// 初始化点击输入上下文。
		/// </summary>
		/// <param name="screenPosition">当前鼠标屏幕坐标。</param>
		/// <param name="worldPosition">当前鼠标世界坐标。</param>
		/// <param name="holdDuration">本次按住持续时间（秒）。</param>
		/// <param name="chargeRatio">归一化蓄力进度 [0, 1]。</param>
		public PointerInputContext(
			Vector2 screenPosition,
			Vector2 worldPosition,
			float holdDuration,
			float chargeRatio)
		{
			ScreenPosition = screenPosition;
			WorldPosition = worldPosition;
			HoldDuration = holdDuration;
			ChargeRatio = chargeRatio;
		}

		/// <summary>
		/// 当前鼠标屏幕坐标。
		/// </summary>
		public Vector2 ScreenPosition { get; }

		/// <summary>
		/// 当前鼠标世界坐标。
		/// </summary>
		public Vector2 WorldPosition { get; }

		/// <summary>
		/// 本次按住持续时间（秒）。
		/// </summary>
		public float HoldDuration { get; }

		/// <summary>
		/// 归一化蓄力进度 [0, 1]。
		/// </summary>
		public float ChargeRatio { get; }
	}

	/// <summary>
	/// 输入系统对外暴露的统一事件入口。
	/// </summary>
	public static class InputEvents
	{
		/// <summary>
		/// 鼠标按下时触发。
		/// </summary>
		public static event Action<PointerInputContext> OnPointerDown;

		/// <summary>
		/// 鼠标按住期间逐帧触发。
		/// </summary>
		public static event Action<PointerInputContext> OnPointerHold;

		/// <summary>
		/// 鼠标松开时触发。
		/// </summary>
		public static event Action<PointerInputContext> OnPointerUp;

		/// <summary>
		/// 鼠标输入被取消时触发。
		/// </summary>
		public static event Action<PointerInputContext> OnPointerCancel;

		/// <summary>
		/// 广播鼠标按下事件。
		/// </summary>
		/// <param name="context">输入上下文。</param>
		public static void RaisePointerDown(PointerInputContext context)
		{
			OnPointerDown?.Invoke(context);
		}

		/// <summary>
		/// 广播鼠标按住事件。
		/// </summary>
		/// <param name="context">输入上下文。</param>
		public static void RaisePointerHold(PointerInputContext context)
		{
			OnPointerHold?.Invoke(context);
		}

		/// <summary>
		/// 广播鼠标松开事件。
		/// </summary>
		/// <param name="context">输入上下文。</param>
		public static void RaisePointerUp(PointerInputContext context)
		{
			OnPointerUp?.Invoke(context);
		}

		/// <summary>
		/// 广播鼠标取消事件。
		/// </summary>
		/// <param name="context">输入上下文。</param>
		public static void RaisePointerCancel(PointerInputContext context)
		{
			OnPointerCancel?.Invoke(context);
		}
	}

	/// <summary>
	/// 统一采样鼠标输入，并将点击/蓄力状态转换为输入事件。
	/// </summary>
	[DisallowMultipleComponent]
	public partial class InputSystem : ViewController
	{
		[Header("输入配置")]
		[SerializeField]
		[Min(0.05f)]
		private float _maxChargeDuration = 1.0f;

		[SerializeField]
		private bool _blockPointerWhenOverUI = true;

		[SerializeField]
		private bool _ignoreInputWhenTimeScaleIsZero = true;

		[SerializeField]
		private bool _cancelChargeWhenFocusLost = true;

		private Camera _mainCamera;
		private bool _isPointerHeld;
		private float _pointerDownTimestamp;
		private int _pointerDownFrame = -1;

		private void Awake()
		{
			TryCacheMainCamera();
		}

		private void Update()
		{
			if (Input.GetMouseButtonDown(0))
			{
				HandlePointerDown();
				Debug.Log("MouseButtonDown!!");
			}

			if (!_isPointerHeld)
			{
				return;
			}

			if (!CanProcessInput())
			{
				CancelPointer();
				return;
			}

			if (Input.GetMouseButtonUp(0))
			{
				HandlePointerUp();
				return;
			}

			if (Time.frameCount > _pointerDownFrame && Input.GetMouseButton(0))
			{
				HandlePointerHold();
			}
		}

		private void OnDisable()
		{
			CancelPointer();
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			if (!hasFocus && _cancelChargeWhenFocusLost)
			{
				CancelPointer();
			}
		}

		private void HandlePointerDown()
		{
			if (!CanProcessInput())
			{
				return;
			}

			if (_blockPointerWhenOverUI && IsPointerOverUI())
			{
				return;
			}

			_isPointerHeld = true;
			_pointerDownTimestamp = Time.unscaledTime;
			_pointerDownFrame = Time.frameCount;
			InputEvents.RaisePointerDown(CreatePointerContext());
            //LogKit.I("[BABEL][Input] Down");
        }

		private void HandlePointerHold()
		{
			InputEvents.RaisePointerHold(CreatePointerContext());
            //LogKit.I("[BABEL][Input] Hold");
        }

		private void HandlePointerUp()
		{
			var context = CreatePointerContext();
			ResetPointerState();
			InputEvents.RaisePointerUp(context);
            //LogKit.I("[BABEL][Input] UP");
        }

		private void CancelPointer()
		{
			if (!_isPointerHeld)
			{
				return;
			}

			var context = CreatePointerContext();
			ResetPointerState();
			InputEvents.RaisePointerCancel(context);
		}

		private bool CanProcessInput()
		{
			if (!TryCacheMainCamera())
			{
				return false;
			}

			return !_ignoreInputWhenTimeScaleIsZero || Time.timeScale > 0f;
		}

		private bool IsPointerOverUI()
		{
			return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
		}

		private bool TryCacheMainCamera()
		{
			if (_mainCamera != null)
			{
				return true;
			}

			_mainCamera = Camera.main;
			return _mainCamera != null;
		}

		private PointerInputContext CreatePointerContext()
		{
			var screenPosition = (Vector2)Input.mousePosition;
			var worldPosition = GetPointerWorldPosition(screenPosition);
			var holdDuration = _isPointerHeld
				? Mathf.Max(0f, Time.unscaledTime - _pointerDownTimestamp)
				: 0f;
			var chargeRatio = Mathf.Clamp01(holdDuration / _maxChargeDuration);
			return new PointerInputContext(
				screenPosition,
				worldPosition,
				holdDuration,
				chargeRatio);
		}

		private Vector2 GetPointerWorldPosition(Vector2 screenPosition)
		{
			if (!TryCacheMainCamera())
			{
				return Vector2.zero;
			}

			var screenPoint = new Vector3(
				screenPosition.x,
				screenPosition.y,
				Mathf.Abs(_mainCamera.transform.position.z));
			var worldPosition = _mainCamera.ScreenToWorldPoint(screenPoint);
			return new Vector2(worldPosition.x, worldPosition.y);
		}

		private void ResetPointerState()
		{
			_isPointerHeld = false;
			_pointerDownTimestamp = 0f;
			_pointerDownFrame = -1;
		}
	}
}
