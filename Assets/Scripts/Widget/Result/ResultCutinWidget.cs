using Commons.Save;
using DG.Tweening;
using Manager.Command;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Widget.Result;
using Zenject;

/// <summary>
/// リザルトのカットインを管理する
/// </summary>
public class ResultCutinWidget : MonoBehaviour
{
    /// <summary>
    /// CanvasGroup
    /// </summary>
    [SerializeField] private CanvasGroup _canvasGroup;
    
    /// <summary>
    /// 背景でスクロールする画像
    /// </summary>
    [SerializeField] private RawImage _stripeImage;
    
    /// <summary>
    /// 表示するメッセージ
    /// </summary>
    [SerializeField] private TextMeshProUGUI _messageText;

    /// <summary>
    /// BackgroundScroller
    /// </summary>
    [SerializeField] private RawImageScroller _backgroundScroller;
    
    /// <summary>
    /// ResultWidgetController
    /// </summary>
    [SerializeField] ResultWidgetController _resultWidgetController;
    
    /// <summary>
    /// AudioManager
    /// </summary>
    [Inject] private AudioManager _audioManager;

    /// <summary>
    /// ストライプのスクロールスピード
    /// </summary>
    private float _defaultStripeScrollSpeed;

    /// <summary>
    /// テキストのスクロールスピード
    /// </summary>
    private float _defaultTextScrollSpeed;
    
    /// <summary>
    /// SaveManager
    /// </summary>
    [Inject] private SaveManager _saveManager;

    /// <summary>
    /// InGameCommandManager
    /// </summary>
    [Inject] private InGameCommandManager _inGameCommandManager;
    
    /// <summary>
    /// Sequence
    /// </summary>
    private Sequence _sequence;

    private void Start()
    {
        _resultWidgetController
            .OnShowResultWidgetAsObservable
            .Subscribe(_=> PlayAnimation())
            .AddTo(this.gameObject);
    }

    /// <summary>
    /// 初期化
    /// </summary>
    private void Initialize()
    {
        _sequence?.Kill();
        _stripeImage.rectTransform.sizeDelta = new Vector2(_stripeImage.rectTransform.sizeDelta.x, 2);
        _messageText.rectTransform.sizeDelta = new Vector2(_messageText.rectTransform.sizeDelta.x, 2);
        _messageText.rectTransform.localScale = new Vector3(1, 0, 1);
        _canvasGroup.alpha = 0;
        _defaultStripeScrollSpeed = _backgroundScroller.ScrollSpeedX;
    }

    /// <summary>
    /// アニメーション再生
    /// </summary>
    private void PlayAnimation()
    {
        Initialize();

        //カットイン表示
        _sequence = DOTween.Sequence()
            .Append(_canvasGroup.DOFade(1f, 0.25f))
            .Join(_messageText.DOText("ゲームクリア！", 0.0f, scrambleMode: ScrambleMode.None))
            .Join(_stripeImage.rectTransform.DOSizeDelta(new Vector2(_stripeImage.rectTransform.sizeDelta.x, 500), 0.25f))
            .Join(_messageText.rectTransform.DOSizeDelta(new Vector2(_messageText.rectTransform.sizeDelta.x, 500), 0.25f))
            .Join(_messageText.rectTransform.DOScaleY(1, 0.25f))
            .AppendCallback(()=>_audioManager.PlaySoundEffect(SoundEffectData.SoundEffect.ResultScroll));

        //スクロールスピードを下げる
        _sequence
            .Append(DOTween.To(() => _backgroundScroller.ScrollSpeedX, x => _backgroundScroller.ScrollSpeedX = x,
                0.5f, 1f))
            .AppendInterval(0.5f);

        //スクロールを止める
        _sequence
            .Append(DOTween.To(() => _backgroundScroller.ScrollSpeedX, x => _backgroundScroller.ScrollSpeedX = x, 0f,
                0.5f))
            .AppendInterval(0.5f)
            .AppendCallback(()=>_audioManager.PlaySoundEffect(SoundEffectData.SoundEffect.ResultUp));

        //文字を飛び出させる
        _sequence
            .Join(_messageText.rectTransform.DOScale(2f, 0.1f))
            .Join(_messageText.DOFade(0f, 0.1f))
            .AppendInterval(0.5f);

        //文字を基の場所に戻す
        _sequence
            .Append(_messageText.DOText($"ネクスト.. {_saveManager.Data.CurrentStageNumber + 1}", 0.0f,
                scrambleMode: ScrambleMode.None))
            .Join(_messageText.rectTransform.DOScale(1f, 0.1f))
            .Join(_messageText.DOFade(1f, 0.1f))
            .AppendCallback(()=>_audioManager.PlaySoundEffect(SoundEffectData.SoundEffect.ResultShake));
        
        //周りを揺らす
        _sequence
            .Append(_stripeImage.rectTransform.DOShakePosition(0.25f, 65f, 50, 50, false, true))
            .Join(_messageText.rectTransform.DOShakePosition(0.25f, 65f, 50, 30, false, true))
            .AppendInterval(0.5f);
        
        //カットインを閉じる
        _sequence
            .Append(_canvasGroup.DOFade(0f, 0.25f))
            .Join(_stripeImage.rectTransform.DOSizeDelta(new Vector2(_stripeImage.rectTransform.sizeDelta.x, 0), 0.25f))
            .Join(_messageText.rectTransform.DOSizeDelta(new Vector2(_messageText.rectTransform.sizeDelta.x, 0), 0.25f))
            .Join(_messageText.rectTransform.DOScaleY(0, 0.25f))
            .OnComplete(() =>
            {
                _resultWidgetController.FinishCutinAnimation();
                _backgroundScroller.ScrollSpeedX = _defaultStripeScrollSpeed;
              });
        
        //アニメーション再生
        _sequence
            .SetEase(Ease.Linear)
            .SetLink(this.gameObject)
            .Play();
    }
}