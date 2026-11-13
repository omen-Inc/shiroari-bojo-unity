// 画面全体の進行(タイトル → 対戦 → 結果)。
// 状態の変更は Engine.Apply だけで行い、表示は Render() で状態から毎回描き直す。
// ルールの判定は書かない(使えるかどうかは Engine.HandOptions / Engine.CanMulligan を見る)。

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShiroariBojo.Core;
using ShiroariBojo.Core.AI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiroariBojo.UI
{
    public sealed class GameController : MonoBehaviour
    {
        [Header("データ")]
        [SerializeField] private TextAsset cardsJson;

        [Header("進行の設定")]
        [Tooltip("CPU が1手打つごとの間(秒)。0 にすると CPU の手番を待たずに一気に進める(テスト用)")]
        [SerializeField] private float cpuStepDelay = 0.9f;
        [Tooltip("「相手のターン」「あなたのターン」の表示時間(秒)")]
        [SerializeField] private float turnBannerDuration = 0.8f;
        [Tooltip("決着から結果画面を出すまでの間(秒)")]
        [SerializeField] private float resultDelay = 0.8f;
        [Tooltip("0 以外なら毎回このシードで対戦する(再現用)。0 なら毎回変える")]
        [SerializeField] private uint fixedSeed;
        [Tooltip("オンにすると、対象のないカードは「使う」ボタンで使う。オフならタップでそのまま使う")]
        [SerializeField] private bool confirmUntargetedCards;

        [Header("画面")]
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject gamePanel;
        [SerializeField] private GameObject resultPanel;

        [Header("タイトル")]
        [SerializeField] private Button pestStartButton;
        [SerializeField] private Button termiteStartButton;

        [Header("相手")]
        [SerializeField] private Image oppBar;
        [SerializeField] private TMP_Text oppNameText;
        [SerializeField] private TMP_Text oppStatsText;
        [SerializeField] private TMP_Text oppGoalText;
        [SerializeField] private GameObject oppThinking;

        [Header("ターン・盤面")]
        [SerializeField] private Image turnBar;
        [SerializeField] private TMP_Text turnText;
        [SerializeField] private PointView[] points = new PointView[Points.Count];
        [SerializeField] private TMP_Text fieldInfoText;

        [Header("自分")]
        [SerializeField] private Image myBar;
        [SerializeField] private TMP_Text myNameText;
        [SerializeField] private TMP_Text myStatsText;
        [SerializeField] private TMP_Text myGoalText;
        [SerializeField] private TMP_Text apText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button useButton;
        [SerializeField] private Button mulliganButton;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private Image endTurnImage;
        [SerializeField] private RectTransform handContainer;
        [SerializeField] private HandCardView handCardPrefab;
        [SerializeField] private TMP_Text handEmptyText;

        [Header("ログ")]
        [SerializeField] private ScrollRect logScroll;
        [SerializeField] private TMP_Text logText;

        [Header("重ねて出すもの")]
        [SerializeField] private GameObject banner;
        [SerializeField] private Image bannerImage;
        [SerializeField] private TMP_Text bannerText;
        [SerializeField] private ChoicePanel choicePanel;

        [Header("結果")]
        [SerializeField] private TMP_Text resultTitleText;
        [SerializeField] private TMP_Text resultReasonText;
        [SerializeField] private TMP_Text resultStatsText;
        [SerializeField] private Button againButton;
        [SerializeField] private Button swapSideButton;
        [SerializeField] private Button backToTitleButton;

        private const int LogLinesShown = 150;

        private CardDatabase _db;
        private GameState _state;
        private CpuPlayer _cpu;
        private Side _human = Side.Pest;
        private uint _seed;

        private int _selectedUid = -1;
        private bool _busy;
        private bool _mulliganOpen;
        private int _logCountShown = -1;
        private bool _scrollLogToBottom;
        private Coroutine _flow;
        private Coroutine _bannerHide;

        // 予知の選択の途中経過
        private string _foresightKey;
        private int _foresightTake = -1;
        private readonly List<int> _foresightOrder = new List<int>();
        // 手札上限で捨てるカード
        private readonly HashSet<int> _discardSelected = new HashSet<int>();
        private int _discardTurn = -1;

        private readonly List<HandCardView> _handViews = new List<HandCardView>();

        // ───────── 外から見る値(テスト・確認用) ─────────
        public GameState State => _state;
        public Side HumanSide => _human;
        public Side CpuSide => _human.Opponent();
        public uint Seed => _seed;
        public bool IsBusy => _busy;
        public int SelectedUid => _selectedUid;
        public float CpuStepDelay { get => cpuStepDelay; set => cpuStepDelay = value; }
        public float TurnBannerDuration { get => turnBannerDuration; set => turnBannerDuration = value; }
        public float ResultDelay { get => resultDelay; set => resultDelay = value; }
        public bool HumanCanAct => _state != null && !_state.IsOver && !_busy && _state.Current == _human;

        private void Awake()
        {
            _db = CardDatabase.FromJson(cardsJson.text);
            pestStartButton.onClick.AddListener(() => StartGame(Side.Pest));
            termiteStartButton.onClick.AddListener(() => StartGame(Side.Termite));
            cancelButton.onClick.AddListener(OnCancel);
            useButton.onClick.AddListener(OnUse);
            mulliganButton.onClick.AddListener(OnMulligan);
            endTurnButton.onClick.AddListener(OnEndTurn);
            againButton.onClick.AddListener(() => StartGame(_human));
            swapSideButton.onClick.AddListener(() => StartGame(_human.Opponent()));
            backToTitleButton.onClick.AddListener(ShowTitle);
        }

        private void Start() => ShowTitle();

        private void LateUpdate()
        {
            // テキストの高さが確定してから一番下(最新)へ送る
            if (!_scrollLogToBottom) return;
            _scrollLogToBottom = false;
            logText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(logScroll.content);
            logScroll.verticalNormalizedPosition = 0f;
        }

        // ───────── 画面の切り替え ─────────

        public void ShowTitle()
        {
            StopFlow();
            _state = null;
            titlePanel.SetActive(true);
            gamePanel.SetActive(false);
            resultPanel.SetActive(false);
            choicePanel.Hide();
            banner.SetActive(false);
        }

        /// <param name="seed">0 なら fixedSeed、それも 0 なら毎回変える</param>
        public void StartGame(Side human, uint seed = 0)
        {
            StopFlow();
            _human = human;
            _seed = seed != 0 ? seed : fixedSeed != 0 ? fixedSeed : NewSeed();
            _state = Engine.NewGame(_db, _seed);
            _cpu = new CpuPlayer(_seed * 2654435761u + 12345u);
            _selectedUid = -1;
            _mulliganOpen = false;
            _foresightKey = null;
            _discardTurn = -1;
            _logCountShown = -1;
            titlePanel.SetActive(false);
            gamePanel.SetActive(true);
            resultPanel.SetActive(false);
            choicePanel.Hide();
            banner.SetActive(false);
            Render();
            Proceed(true);
        }

        private static uint NewSeed()
        {
            uint s = (uint)(DateTime.Now.Ticks & 0xffffffff);
            return s == 0 ? 1u : s;
        }

        private void StopFlow()
        {
            if (_flow != null) StopCoroutine(_flow);
            if (_bannerHide != null) StopCoroutine(_bannerHide);
            _flow = null;
            _bannerHide = null;
            _busy = false;
        }

        // ───────── 進行 ─────────

        /// <summary>今の手番に応じて、CPU を動かすか、人の入力を待つか、結果を出す</summary>
        private void Proceed(bool turnChanged)
        {
            if (_state == null) return;
            if (_state.IsOver)
            {
                Render();
                if (resultDelay > 0f) _flow = StartCoroutine(ShowResultLater());
                else ShowResult();
                return;
            }
            if (_state.Current == CpuSide)
            {
                if (cpuStepDelay <= 0f)
                {
                    while (!_state.IsOver && _state.Current == CpuSide) StepCpu();
                    Proceed(true);
                }
                else _flow = StartCoroutine(CpuTurn());
                return;
            }
            if (turnChanged && turnBannerDuration > 0f) FlashBanner("あなたのターン", UiPalette.SideColor(_human), turnBannerDuration);
            Render();
        }

        private IEnumerator CpuTurn()
        {
            _busy = true;
            _selectedUid = -1;
            Render();
            if (turnBannerDuration > 0f)
            {
                ShowBanner("相手のターン", UiPalette.SideColor(CpuSide));
                yield return new WaitForSeconds(turnBannerDuration);
            }
            while (_state != null && !_state.IsOver && _state.Current == CpuSide)
            {
                yield return new WaitForSeconds(cpuStepDelay);
                var text = StepCpu();
                Render();
                if (!string.IsNullOrEmpty(text)) ShowBanner(text, UiPalette.SideColor(CpuSide));
            }
            yield return new WaitForSeconds(Mathf.Min(cpuStepDelay, 0.6f));
            banner.SetActive(false);
            _busy = false;
            _flow = null;
            Proceed(true);
        }

        /// <summary>CPU に1手打たせる。戻り値は演出用の文(人の陣営から見える文だけ)</summary>
        private string StepCpu()
        {
            var action = _cpu.ChooseAction(new PlayerView(_state, CpuSide));
            int from = _state.Log.Count;
            _state = Engine.Apply(_state, action);
            var lines = new List<string>();
            for (int i = from; i < _state.Log.Count; i++)
            {
                var e = _state.Log[i];
                if (e.Type == GameEventType.TurnStarted || e.Type == GameEventType.TurnEnded) continue;
                var t = e.TextFor(_human);
                if (!string.IsNullOrEmpty(t)) lines.Add(t);
            }
            if (lines.Count == 0 && action.Type == ActionType.EndTurn) lines.Add("ターン終了");
            if (lines.Count == 0) return null;
            return "CPU:" + lines[0] + (lines.Count > 1 ? "\n<size=75%>" + string.Join("\n", lines.Skip(1)) + "</size>" : "");
        }

        private IEnumerator ShowResultLater()
        {
            yield return new WaitForSeconds(resultDelay);
            _flow = null;
            ShowResult();
        }

        /// <summary>人の行動を適用する(画面のボタンからも、確認用のスクリプトからもここを通る)</summary>
        public bool ApplyHuman(GameAction action)
        {
            if (!HumanCanAct) return false;
            if (!Engine.IsLegal(_state, action, out var reason))
            {
                Debug.LogWarning($"使えない行動です: {action}({reason})");
                return false;
            }
            _state = Engine.Apply(_state, action);
            _selectedUid = -1;
            _mulliganOpen = false;
            if (_state.IsOver || _state.Current != _human) Proceed(true);
            else Render();
            return true;
        }

        // ───────── 人の入力 ─────────

        public void OnHandCardClicked(int uid)
        {
            if (!HumanCanAct || _state.Phase != Phase.Main) return;
            if (_selectedUid == uid)
            {
                _selectedUid = -1;
                Render();
                return;
            }
            _selectedUid = uid;
            var o = SelectedOption();
            if (o != null && o.Usable && !o.NeedsTarget && !confirmUntargetedCards)
            {
                ApplyHuman(GameAction.Play(uid));
                return;
            }
            Render();
        }

        public void OnPointClicked(int index)
        {
            if (!HumanCanAct || _state.Phase != Phase.Main) return;
            var o = SelectedOption();
            if (o == null || !o.Usable || !o.NeedsTarget || !o.Targets.Contains(index)) return;
            ApplyHuman(GameAction.Play(o.Uid, index));
        }

        public void OnCancel()
        {
            _selectedUid = -1;
            Render();
        }

        public void OnUse()
        {
            var o = SelectedOption();
            if (o == null || !o.Usable || o.NeedsTarget) return;
            ApplyHuman(GameAction.Play(o.Uid));
        }

        public void OnEndTurn()
        {
            if (!HumanCanAct || _state.Phase != Phase.Main) return;
            ApplyHuman(GameAction.EndTurn());
        }

        public void OnMulligan()
        {
            if (!HumanCanAct || !Engine.CanMulligan(_state)) return;
            _selectedUid = -1;
            _mulliganOpen = true;
            Render();
        }

        private PlayOption SelectedOption()
        {
            if (_state == null || _selectedUid < 0) return null;
            return Engine.HandOptions(_state, _human).FirstOrDefault(o => o.Uid == _selectedUid);
        }

        // ───────── 描画 ─────────

        public void Render()
        {
            if (_state == null) return;
            var s = _state;
            var me = s.Player(_human);
            var opp = s.Player(CpuSide);
            bool myTurn = !s.IsOver && s.Current == _human;
            bool canAct = HumanCanAct;

            // 相手・自分のバー
            oppBar.color = UiPalette.SideSoft(CpuSide);
            oppNameText.text = $"CPU <size=70%><color={UiPalette.Tag(UiPalette.SideColor(CpuSide))}>{CpuSide.Label()}</color></size>";
            oppStatsText.text = StatsLine(opp);
            oppGoalText.text = GoalLine(CpuSide);
            oppThinking.SetActive(!s.IsOver && s.Current == CpuSide);

            myBar.color = UiPalette.SideSoft(_human);
            myNameText.text = $"あなた <size=70%><color={UiPalette.Tag(UiPalette.SideColor(_human))}>{_human.Label()}</color></size>";
            myStatsText.text = StatsLine(me);
            myGoalText.text = GoalLine(_human);
            apText.text = myTurn ? ApLine(me.Ap, s.Rules.ActionPoints) : $"<color={UiPalette.Tag(UiPalette.Muted)}>行動力 -</color>";

            // ターン
            if (s.IsOver)
            {
                turnBar.color = UiPalette.Ink;
                turnText.text = $"ターン {s.Turn} ・ 対戦終了";
            }
            else
            {
                turnBar.color = UiPalette.SideColor(s.Current);
                turnText.text = $"ターン {s.Turn} ・ {(s.Current == _human ? "あなた" : "CPU")}({s.Current.Label()})の番";
            }

            // 盤面
            var options = Engine.HandOptions(s, _human);
            var sel = _selectedUid >= 0 ? options.FirstOrDefault(o => o.Uid == _selectedUid) : null;
            if (sel == null) _selectedUid = -1;
            var targets = sel != null && sel.Usable && sel.NeedsTarget && canAct && s.Phase == Phase.Main ? sel.Targets : (IReadOnlyList<int>)new int[0];
            foreach (var pv in points) pv.Bind(s, targets.Contains(pv.PointIndex), OnPointClicked);
            fieldInfoText.text = FieldInfo(s);

            // 手札
            RenderHand(options, canAct && s.Phase == Phase.Main, myTurn && s.Phase == Phase.Main);

            // メッセージとボタン
            messageText.text = Message(s, sel, canAct);
            cancelButton.gameObject.SetActive(sel != null && canAct);
            useButton.gameObject.SetActive(confirmUntargetedCards && sel != null && sel.Usable && !sel.NeedsTarget && canAct);
            mulliganButton.gameObject.SetActive(canAct && Engine.CanMulligan(s));
            endTurnButton.interactable = canAct && s.Phase == Phase.Main;
            endTurnImage.color = UiPalette.SideColor(_human);

            RenderLog();
            RenderChoice();
        }

        private void RenderHand(List<PlayOption> options, bool interactable, bool showReason)
        {
            while (_handViews.Count < options.Count) _handViews.Add(Instantiate(handCardPrefab, handContainer));
            for (int i = 0; i < _handViews.Count; i++)
            {
                bool on = i < options.Count;
                _handViews[i].gameObject.SetActive(on);
                if (on) _handViews[i].Bind(options[i], options[i].Uid == _selectedUid, interactable, showReason, OnHandCardClicked);
            }
            handEmptyText.gameObject.SetActive(options.Count == 0);
        }

        private string StatsLine(PlayerState p) =>
            $"山札 <b>{p.Deck.Count}</b> ・ 手札 <b>{p.Hand.Count}</b> ・ 捨て札 <b>{p.Discard.Count}</b>";

        private string GoalLine(Side side)
        {
            if (side == Side.Termite)
                return $"完全侵食 <b>{_state.DestroyedCount}/{_state.Rules.PointsToWin}</b>";
            return $"シロアリ山札 残り<b>{_state.Player(Side.Termite).Deck.Count}</b>";
        }

        private static string ApLine(int ap, int max)
        {
            var sb = new StringBuilder("行動力 ");
            for (int k = 0; k < max; k++)
                sb.Append(k < ap ? $"<color={UiPalette.Tag(UiPalette.Gold)}>●</color>" : $"<color={UiPalette.Tag(UiPalette.Line)}>●</color>");
            sb.Append($" {ap}/{max}");
            return sb.ToString();
        }

        private static string FieldInfo(GameState s)
        {
            var sb = new StringBuilder("完全侵食 ");
            for (int k = 0; k < s.Rules.PointsToWin; k++)
                sb.Append(k < s.DestroyedCount ? $"<color={UiPalette.Tag(UiPalette.Danger)}>■</color>" : $"<color={UiPalette.Tag(UiPalette.Line)}>■</color>");
            sb.Append($" {s.DestroyedCount}/{s.Rules.PointsToWin}\n<size=80%><color={UiPalette.Tag(UiPalette.Tree)}>");
            sb.Append(s.TreeFallen
                ? $"庭の枯れ木が倒れた! 全ての場所が{s.Rules.AfterTreeFallRequired}枚で完全侵食"
                : $"庭の枯れ木は{s.Rules.GardenTreeRequired}枚で倒れ、倒れると全ての場所が{s.Rules.AfterTreeFallRequired}枚で完全侵食");
            sb.Append("</color></size>");
            return sb.ToString();
        }

        private string Message(GameState s, PlayOption sel, bool canAct)
        {
            if (s.IsOver) return "対戦終了";
            if (s.Current != _human) return $"<color={UiPalette.Tag(UiPalette.Muted)}>相手のターンです。CPU が考えています…</color>";
            if (s.Phase == Phase.ForesightChoice) return "予知:手札に加えるカードを選んでください";
            if (s.Phase == Phase.HandLimitDiscard) return $"手札が{s.Rules.HandLimit}枚を超えています。捨てるカードを選んでください";
            if (!canAct) return "";
            if (sel == null)
            {
                var msg = $"カードを選んでください(行動力 残り{s.Player(_human).Ap})";
                if (Engine.CanMulligan(s)) msg += $"\n<size=80%><color={UiPalette.Tag(UiPalette.Muted)}>このターンはまだ1枚も使っていません。「1枚捨てて引き直す」でターンを終えられます</color></size>";
                return msg;
            }
            var d = sel.Def;
            var sb = new StringBuilder($"<b>{d.Name}</b> <size=80%>コスト{sel.Cost} ・ {HandCardView.KindLabel(d)}</size>");
            if (!sel.Usable) sb.Append($"  <color={UiPalette.Tag(UiPalette.Danger)}><size=85%>{sel.Reason}</size></color>");
            else if (sel.NeedsTarget) sb.Append($"  <color={UiPalette.Tag(UiPalette.GoldText)}><size=85%>光っているポイントを選んでください</size></color>");
            else if (confirmUntargetedCards) sb.Append("  <size=85%>「使う」で発動</size>");
            if (!string.IsNullOrEmpty(d.EffectText)) sb.Append($"\n<size=78%><color={UiPalette.Tag(UiPalette.Muted)}>{d.EffectText}</color></size>");
            return sb.ToString();
        }

        private void RenderLog()
        {
            if (_state.Log.Count == _logCountShown) return;
            _logCountShown = _state.Log.Count;
            var lines = new List<string>();
            foreach (var e in _state.Log)
            {
                var t = e.TextFor(_human);
                if (string.IsNullOrEmpty(t)) continue;
                if (e.Type == GameEventType.TurnStarted)
                    lines.Add($"<color={UiPalette.Tag(UiPalette.SideOnDark(e.Side))}><b>{t}</b></color>");
                else if (e.Type == GameEventType.TurnEnded)
                    continue;
                else if (e.Type == GameEventType.GameStarted || e.Type == GameEventType.GameOver)
                    lines.Add($"<b>{t}</b>");
                else
                {
                    var who = e.Side == _human ? "あなた" : "CPU";
                    lines.Add($"<color={UiPalette.Tag(UiPalette.SideOnDark(e.Side))}>{who}</color> {t}");
                }
            }
            if (lines.Count > LogLinesShown) lines = lines.Skip(lines.Count - LogLinesShown).ToList();
            logText.text = string.Join("\n", lines);
            _scrollLogToBottom = true;
        }

        // ───────── 選択シート(予知・手札上限・手詰まり防止) ─────────

        private void RenderChoice()
        {
            var s = _state;
            bool mine = !s.IsOver && s.Current == _human && !_busy;
            if (mine && s.Phase == Phase.ForesightChoice) RenderForesight();
            else if (mine && s.Phase == Phase.HandLimitDiscard) RenderDiscard();
            else if (mine && _mulliganOpen && Engine.CanMulligan(s)) RenderMulligan();
            else
            {
                _mulliganOpen = false;
                choicePanel.Hide();
            }
        }

        private string CardLabel(string defId)
        {
            var d = _db.Get(defId);
            var color = UiPalette.Tag(UiPalette.UserColor(d.User));
            return $"<color={color}><b>{d.Name}</b></color> <size=75%><color={UiPalette.Tag(UiPalette.Muted)}>コスト{(d.Cost.HasValue ? d.Cost.Value.ToString() : "-")} ・ {HandCardView.KindLabel(d)}</color></size>";
        }

        private void RenderForesight()
        {
            var cards = _state.ForesightCards;
            var key = string.Join(",", cards.Select(c => c.Uid));
            if (key != _foresightKey)
            {
                _foresightKey = key;
                _foresightTake = -1;
                _foresightOrder.Clear();
            }
            var rows = new List<ChoiceRow>();
            if (_foresightTake < 0)
            {
                foreach (var c in cards)
                {
                    int uid = c.Uid;
                    rows.Add(new ChoiceRow
                    {
                        Label = CardLabel(c.DefId),
                        OnClick = () =>
                        {
                            _foresightTake = uid;
                            _foresightOrder.Clear();
                            _foresightOrder.AddRange(cards.Where(x => x.Uid != uid).Select(x => x.Uid));
                            if (_foresightOrder.Count <= 1) ApplyHuman(GameAction.ChooseForesight(uid, _foresightOrder.ToList()));
                            else Render();
                        },
                    });
                }
                choicePanel.Show($"予知:山札の上{cards.Count}枚", "手札に加える1枚を選んでください。残りは山札の下に置きます", rows, null, false, null);
                return;
            }
            for (int k = 0; k < _foresightOrder.Count; k++)
            {
                int idx = k;
                var c = cards.First(x => x.Uid == _foresightOrder[idx]);
                rows.Add(new ChoiceRow
                {
                    Label = $"{k + 1}. {CardLabel(c.DefId)}",
                    ShowUp = true,
                    OnUp = idx > 0 ? () =>
                    {
                        (_foresightOrder[idx - 1], _foresightOrder[idx]) = (_foresightOrder[idx], _foresightOrder[idx - 1]);
                        Render();
                    } : (Action)null,
                });
            }
            var take = cards.First(x => x.Uid == _foresightTake);
            choicePanel.Show("山札の下に置く順番",
                $"手札に加える:{_db.Get(take.DefId).Name}\nリストの下にあるカードほど、山札の底になります",
                rows, "この順で山札の下へ", true,
                () => ApplyHuman(GameAction.ChooseForesight(_foresightTake, _foresightOrder.ToList())),
                "選び直す", () => { _foresightTake = -1; Render(); });
        }

        private void RenderDiscard()
        {
            if (_discardTurn != _state.Turn)
            {
                _discardTurn = _state.Turn;
                _discardSelected.Clear();
            }
            int need = _state.HandLimitDiscardCount;
            var hand = _state.Player(_human).Hand;
            _discardSelected.RemoveWhere(u => hand.All(c => c.Uid != u));
            var rows = new List<ChoiceRow>();
            foreach (var c in hand)
            {
                int uid = c.Uid;
                bool on = _discardSelected.Contains(uid);
                rows.Add(new ChoiceRow
                {
                    Label = (on ? "<color=#bf3a2a>捨てる</color>  " : "") + CardLabel(c.DefId),
                    Selected = on,
                    OnClick = () =>
                    {
                        if (!_discardSelected.Remove(uid) && _discardSelected.Count < need) _discardSelected.Add(uid);
                        Render();
                    },
                });
            }
            choicePanel.Show($"手札が{_state.Rules.HandLimit}枚を超えています",
                $"捨てるカードを{need}枚選んでください({_discardSelected.Count}/{need})", rows,
                "捨ててターン終了", _discardSelected.Count == need,
                () =>
                {
                    var picks = hand.Where(c => _discardSelected.Contains(c.Uid)).Select(c => c.Uid).ToList();
                    ApplyHuman(GameAction.DiscardForHandLimit(picks));
                });
        }

        private void RenderMulligan()
        {
            var rows = new List<ChoiceRow>();
            foreach (var c in _state.Player(_human).Hand)
            {
                int uid = c.Uid;
                rows.Add(new ChoiceRow { Label = $"{CardLabel(c.DefId)} を捨てる", OnClick = () => ApplyHuman(GameAction.EndTurn(uid)) });
            }
            choicePanel.Show("1枚捨てて引き直す",
                "このターンは1枚も使っていないので、手札を1枚捨てて1枚引けます。選ぶとターンが終わります", rows,
                null, false, null, "やめる", () => { _mulliganOpen = false; Render(); });
        }

        // ───────── 演出・結果 ─────────

        private void ShowBanner(string text, Color color)
        {
            if (_bannerHide != null) { StopCoroutine(_bannerHide); _bannerHide = null; }
            banner.SetActive(true);
            bannerImage.color = color;
            bannerText.text = text;
        }

        private void FlashBanner(string text, Color color, float seconds)
        {
            ShowBanner(text, color);
            _bannerHide = StartCoroutine(HideBannerLater(seconds));
        }

        private IEnumerator HideBannerLater(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            banner.SetActive(false);
            _bannerHide = null;
        }

        private void ShowResult()
        {
            if (_state == null || !_state.IsOver) return;
            choicePanel.Hide();
            banner.SetActive(false);
            resultPanel.SetActive(true);
            var w = _state.Winner;
            if (w == null)
            {
                resultTitleText.text = "引き分け";
                resultTitleText.color = UiPalette.Ink;
            }
            else if (w.Value == _human)
            {
                resultTitleText.text = "勝利!";
                resultTitleText.color = UiPalette.Pest;
            }
            else
            {
                resultTitleText.text = "敗北…";
                resultTitleText.color = UiPalette.Danger;
            }
            var over = _state.Log.LastOrDefault(e => e.Type == GameEventType.GameOver);
            var winner = w.HasValue ? $"<color={UiPalette.Tag(UiPalette.SideColor(w.Value))}><b>{w.Value.Label()}の勝ち</b></color>\n" : "";
            resultReasonText.text = winner + (over != null ? over.Text : ReasonText(_state.WinReason));
            resultStatsText.text =
                $"あなた:{_human.Label()}\n完全侵食 {_state.DestroyedCount}/{_state.Rules.PointsToWin} ・ {_state.Turn}ターン\n" +
                $"庭の枯れ木:{(_state.TreeFallen ? "倒れた" : "倒れていない")} ・ シロアリ山札 残り{_state.Player(Side.Termite).Deck.Count}";
        }

        private static string ReasonText(WinReason r) =>
            r == WinReason.Infestation ? "5ポイントを完全侵食した" :
            r == WinReason.DeckOut ? "シロアリの山札が尽きるまで守りきった" :
            r == WinReason.TurnLimit ? "ターン数の上限に達した" : "";
    }
}
