using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BlackHole;
using KModkit;
using UnityEngine;
using Rnd = UnityEngine.Random;
using System.Reflection;

/// <summary>
/// On the Subject of White Hole
/// Created by Anonymous
/// </summary>
public class WhiteHoleModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMRuleSeedable RuleSeedable;

    public Texture[] SwirlTextures;
    public GameObject ContainerTemplate;
    public MeshRenderer ImageTemplate;
    public TextMesh TextTemplate;
    public Transform SwirlContainer;
    public KMSelectable Selectable;
    public KMSelectable[] Arrows; //Standard trig order

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private bool _isSolved = false;

    private int _lastSolved = 0;

    const float _planetSize = .5f;

    private readonly int[][] _grid = new[] { 3, 4, 1, 0, 2, 3, 1, 2, 0, 4, 1, 3, 0, 2, 4, 1, 2, 3, 4, 0, 3, 2, 4, 2, 1, 3, 0, 0, 1, 4, 4, 0, 0, 1, 3, 4, 2, 2, 1, 3, 1, 2, 1, 3, 0, 0, 4, 3, 4, 2, 4, 0, 2, 3, 4, 1, 3, 0, 2, 1, 2, 1, 3, 1, 3, 0, 4, 4, 0, 2, 2, 4, 4, 0, 0, 2, 1, 1, 3, 3, 0, 1, 3, 4, 2, 2, 0, 4, 3, 1, 0, 3, 2, 4, 1, 4, 3, 1, 2, 0 }
        .Split(10).Select(gr => gr.ToArray()).ToArray();

    private readonly Color[] _colors = new[] {
        new Color(0xe7/255f, 0x09/255f, 0x09/255f),
        new Color(0xed/255f, 0x80/255f, 0x0c/255f),
        new Color(0xde/255f, 0xda/255f, 0x16/255f),
        new Color(0x17/255f, 0xb1/255f, 0x29/255f),
        new Color(0x10/255f, 0xa0/255f, 0xa8/255f),
        new Color(0x28/255f, 0x26/255f, 0xff/255f),
        new Color(0xbb/255f, 0x0d/255f, 0xb0/255f)
    };

    sealed class WhiteHoleBombInfo
    {
        public List<WhiteHoleModule> UnlinkedModules = new List<WhiteHoleModule>();
        public Dictionary<WhiteHoleModule, KMBombModule> ModulePairs = new Dictionary<WhiteHoleModule, KMBombModule>();
        public List<int> SolutionCode;
        public int DigitsEntered = 0;
        public int DigitsExpected;
        public WhiteHoleModule LastDigitEntered;
        public int BlackHoleDigitsEntered = 0;
        public int StartDirection;
        public bool Clockwise;
        public List<int> BlackHoleCode;
        public Func<float, float> SeedRule = f => f;
    }

    private static readonly Dictionary<string, WhiteHoleBombInfo> _infos = new Dictionary<string, WhiteHoleBombInfo>();
    private WhiteHoleBombInfo _info;
    private int _digitsEntered;
    private int _digitsExpected;

    private Coroutine countdown;

    private int wholeswirlcount = 0;
    private float swirlcount = 0;
    private float speed;
    private float target;

    private float currentAngle = 0f;
    private bool isCurrentAngleSet = false;

    private static Type BHMType;

    private bool _SuppressStrikes = false;

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        var serialNumber = Bomb.GetSerialNumber();
        if(!_infos.ContainsKey(serialNumber))
            _infos[serialNumber] = new WhiteHoleBombInfo();
        _info = _infos[serialNumber];
        _info.UnlinkedModules.Add(this);

        Bomb.OnBombExploded += delegate { _infos.Clear(); };
        Bomb.OnBombSolved += delegate
        {
            // This check is necessary because this delegate gets called even if another bomb in the same room got solved instead of this one
            if(Bomb.GetSolvedModuleNames().Count == Bomb.GetSolvableModuleNames().Count)
                _infos.Remove(serialNumber);
        };

        speed = Rnd.Range(10f, 30f);
        target = Rnd.Range(.99f, 1.05f);

        _lastSolved = 0;
        _digitsEntered = 0;
        _digitsExpected = 7;

        StartCoroutine(Initialize(serialNumber));

        Module.OnActivate += Activate;
    }

    private ParticleSystem particles;

    private void Activate()
    {
        for(int i = 0; i < Arrows.Length; i++)
        {
            int j = i;
            Arrows[i].OnInteract += () => ArrowPress(j);
        }
        Selectable.OnInteract += HolePress;
    }

    private IEnumerator CreateSwirl(int ix)
    {
        GameObject cont = Instantiate(ContainerTemplate, SwirlContainer);
        cont.transform.localScale = new Vector3(0f, 0f, 0f);
        cont.SetActive(true);
        for(int i = 0; i < 7; i++)
        {
            var ct = Instantiate(ContainerTemplate, cont.transform);
            ct.transform.localPosition = new Vector3(0f, 0f, 0f);
            ct.transform.localEulerAngles = new Vector3(0f, 0f, 360f / 7 * i);
            ct.SetActive(true);

            var mr = Instantiate(ImageTemplate, ct.transform);
            mr.material.mainTexture = SwirlTextures[ix];
            mr.material.renderQueue = 2700 + i + 7 * ix;
            mr.transform.localPosition = new Vector3((250 - 201 - 70 / 2) / 500f, (250 - 31 - 32 / 2) / 500f, 0);
            mr.transform.localScale = new Vector3(70f / 500, 32f / 500, 1);
            mr.gameObject.SetActive(true);
        }
        StartCoroutine(SwirlOut(cont, Rnd.Range(.99f, 1.05f)));
        float rotationSpeed = Rnd.Range(10f, 30f);
        while(true)
        {
            cont.transform.localEulerAngles -= new Vector3(0f, 0f, rotationSpeed * Time.deltaTime);
            yield return null;
        }
    }

    private void Update()
    {
        if(!_isSolved && _info.UnlinkedModules.Contains(this))
        {
            var solved = Bomb.GetSolvedModuleNames().Where(mn => mn != "Black Hole").Count();
            if(solved != _lastSolved)
            {
                _lastSolved = solved;
                if(_info.LastDigitEntered == this)
                {
                    Debug.LogFormat(@"[White Hole #{0}] You solved another module, so 2 digits are slashed from the code.", _moduleId);
                    _info.LastDigitEntered = null;
                    if(_digitsExpected > _digitsEntered + 1) { StartCoroutine(CreateSwirl(6 - wholeswirlcount)); wholeswirlcount++; }
                    if(_digitsExpected > _digitsEntered + 2) { StartCoroutine(CreateSwirl(6 - wholeswirlcount)); wholeswirlcount++; }

                    _info.DigitsExpected = Math.Max(_info.DigitsEntered + 1, _info.DigitsExpected - 2);
                    _digitsExpected = Math.Max(_digitsEntered + 1, _digitsExpected - 2);
                }
            }
        }
    }

    private bool HolePress()
    {
        if(_isSolved)
            return false;
        if(_info.UnlinkedModules.Contains(this))
        {
            var cont = Instantiate(ContainerTemplate);
            cont.transform.parent = SwirlContainer.parent;
            cont.transform.localScale = new Vector3(_planetSize, _planetSize, _planetSize);
            cont.gameObject.SetActive(true);

            var tm = Instantiate(TextTemplate);
            tm.text = _info.SolutionCode[_info.DigitsEntered].ToString();
            tm.color = _colors[_digitsEntered];
            tm.transform.parent = cont.transform;
            tm.transform.localPosition = new Vector3(0, 0, 0);
            tm.transform.localRotation = Quaternion.identity;
            tm.transform.localScale = new Vector3(.07f / _planetSize, .125f / _planetSize, .125f / _planetSize);
            tm.gameObject.SetActive(true);

            cont.transform.parent = Selectable.transform;
            cont.transform.localPosition = new Vector3(0f, 0f, 0f);
            cont.transform.localEulerAngles = new Vector3(0f, 0f, Rnd.Range(0f, 360f));

            Debug.LogFormat("[White Hole #{0}] Obtained a number: {1}", _moduleId, _info.SolutionCode[_info.DigitsEntered]);
            _info.LastDigitEntered = this;

            StartCoroutine(NumberOut(cont, false));
            _info.DigitsEntered++;
            _digitsEntered++;
            if(_digitsEntered >= _digitsExpected)
            {
                Debug.LogFormat("[White Hole #{0}] Module solved!", _moduleId);
                _isSolved = true;
                Module.HandlePass();
            }
        }
        else
        {
            if(_digitsEntered + 1 > _digitsExpected)
            {
                Debug.LogFormat("[White Hole {0}] Module solved! Inputs now disabled.", _moduleId);
                _isSolved = true;
                Module.HandlePass();
            }
            else
            {
                Debug.LogFormat("[White Hole {0}] I struck, as not enough directions have been entered.", _moduleId);
                if(!_SuppressStrikes)
                    Module.HandleStrike();
            }
        }
        return false;
    }

    private bool ArrowPress(int arrowId)
    {
        Arrows[arrowId].AddInteractionPunch(0.1f);
        if(isCurrentAngleSet || _isSolved)
            return false;
        currentAngle = 22.5f + 45f * arrowId;
        particles.transform.localEulerAngles = new Vector3(0f, 0f, 45f * arrowId);
        particles.Play();
        Debug.LogFormat("[White Hole #{0}] You set the angle to {1} degrees.", _moduleId, currentAngle);
        isCurrentAngleSet = true;
        return false;
    }

    private IEnumerator Initialize(string serialNumber)
    {
        yield return null;
        particles = GetComponentInChildren<ParticleSystem>();
        particles.transform.localScale *= transform.lossyScale.x;
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if(_info.ModulePairs.Count == 0)
        {
            List<KMBombModule> blackHoles = transform.root.GetComponentsInChildren<KMBombModule>().Where(m => m.ModuleDisplayName == "Black Hole").ToList();

            int pairs = Math.Min(blackHoles.Count, _info.UnlinkedModules.Count);

            if(pairs != 0)
            {
                for(int i = 0; i < pairs; i++)
                    _info.ModulePairs.Add(_info.UnlinkedModules[i], blackHoles[i]);
                _info.UnlinkedModules = _info.UnlinkedModules.Skip(pairs).ToList();
                if(BHMType == null)
                    BHMType = blackHoles.First().GetComponents<Component>().Where(c => c.GetType().Name == "BlackHoleModule").First().GetType();
                for(int i = pairs; i < blackHoles.Count; i++)
                    BHMType.GetField("OnNumberDisappear", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(blackHoles[i].GetComponents<Component>().Where(c => c.GetType().Name == "BlackHoleModule").First(), new Func<GameObject, bool>(x => { if(x.GetComponentInChildren<TextMesh>().color != Color.white) _info.BlackHoleDigitsEntered++; return true; }));
            }
        }

        yield return null;

        if(_info.UnlinkedModules.Contains(this))
        {
            Debug.LogFormat("[White Hole #{0}] I am unlinked.", _moduleId);
        }
        else
        {
            Component BHM = _info.ModulePairs[this].GetComponents<Component>().Where(c => c.GetType().Name == "BlackHoleModule").First();
            if(BHMType == null)
                BHMType = BHM.GetType();
            BHMType.GetField("OnSwirlDisappear", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(BHM, new Func<GameObject, bool>(ObtainSwirl));
            BHMType.GetField("OnNumberDisappear", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(BHM, new Func<GameObject, bool>(ObtainNumber));
            Debug.LogFormat("[White Hole #{0}] I am linked to Black Hole #{1}.", _moduleId, (int)BHMType.GetField("_moduleId", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(BHM));
        }

        // Only calculate the solution code once per bomb (Copied from Black Hole)
        if(_info.SolutionCode == null)
        {
            // SEEDED RULE GENERATION STARTS HERE
            var rnd = RuleSeedable.GetRNG();
            Debug.LogFormat("[White Hole #{0}] Using rule seed: {1}", _moduleId, rnd.Seed);

            // The RNG has the unfortunate property that the nth number generated is fairly predictable for many values of n.
            // Therefore, we inject more randomness by skipping a random(!) number of samples.
            var num = rnd.Next(0, 10);
            for(var i = 0; i < num; i++)
                rnd.NextDouble();
            new[] { 'a', 'b', 'c', 'd', new[] { 'e', 'f' }[rnd.Next(0, 2)], 'g' }.OrderBy(_ => rnd.NextDouble()).ToArray();

            rnd.Next(0, 3);

            // Starting position in the grid
            int x, y;
            var serialNumberDigits = serialNumber.Where(ch => char.IsNumber(ch));
            switch(rnd.Next(0, 6))
            {
                case 0:
                    x = serialNumberDigits.First() - '0';
                    y = serialNumberDigits.Skip(1).First() - '0';
                    break;
                case 1:
                    x = serialNumberDigits.Skip(1).First() - '0';
                    y = serialNumberDigits.First() - '0';
                    break;
                case 2:
                    x = serialNumber[5] - '0';
                    y = serialNumber[2] - '0';
                    break;
                case 3:
                    x = serialNumber[2] - '0';
                    y = serialNumber[5] - '0';
                    break;
                case 4:
                    x = serialNumberDigits.First() - '0';
                    y = serialNumberDigits.Last() - '0';
                    break;
                default:
                    x = serialNumberDigits.Last() - '0';
                    y = serialNumberDigits.First() - '0';
                    break;
            }

            // Initial direction
            int dir = new[] { 2, 4, 6, 0 }[rnd.Next(0, 4)]; // 0 = north, 1 = NE, etc.
            var initialClockwise = rnd.Next(0, 2) != 0;
            var clockwise = _info.Clockwise = rnd.Next(0, 2) == 0; // Reversed from Black Hole
            var widgetCount = new[] {
                Bomb.GetBatteryCount() + Bomb.GetPortCount(),
                Bomb.GetBatteryCount() + Bomb.GetIndicators().Count(),
                Bomb.GetBatteryHolderCount() + Bomb.GetPortCount(),
                Bomb.GetBatteryHolderCount() + Bomb.GetIndicators().Count(),
                Bomb.GetPortCount() + Bomb.GetIndicators().Count(),
                Bomb.GetPortCount(),
                Bomb.GetIndicators().Count(),
                Bomb.GetBatteryCount(),
                Bomb.GetBatteryHolderCount()
            }[rnd.Next(0, 9)];
            Debug.LogFormat("<White Hole #{0}> Misc: X: {1} Y: {2} Dir: {3} WC: {4} IC: {5} C: {6}", _moduleId, x.ToString(), y.ToString(), dir.ToString(), widgetCount.ToString(), initialClockwise.ToString(), clockwise.ToString());
            if(initialClockwise)
                dir = (dir + widgetCount) % 8;
            else
                dir = ((dir - widgetCount) % 8 + 8) % 8;

            _info.StartDirection = dir;

            // Compute the full solution code
            _info.DigitsExpected = _info.UnlinkedModules.Count * 7;
            _info.SolutionCode = new List<int>();
            _info.BlackHoleCode = new List<int>();

            int x2 = x;
            int y2 = y;

            for(int i = 0; i < _info.DigitsExpected; i++)
            {
                var digit = 0;
                for(int j = 0; j < i + 1; j++)
                {
                    digit = (digit + _grid[y][x]) % 5;
                    if(dir == 1 || dir == 2 || dir == 3)
                        x = (x + 1) % 10;
                    else if(dir == 5 || dir == 6 || dir == 7)
                        x = (x + 9) % 10;
                    if(dir == 7 || dir == 0 || dir == 1)
                        y = (y + 9) % 10;
                    else if(dir == 3 || dir == 4 || dir == 5)
                        y = (y + 1) % 10;
                }
                _info.SolutionCode.Add(digit);
                dir = (dir + (clockwise ? 1 : 7)) % 8;
            }

            dir = _info.StartDirection;
            x = x2;
            y = y2;

            for(int i = 0; i < transform.root.GetComponentsInChildren<KMBombModule>().Where(m => m.ModuleDisplayName == "Black Hole").Count() * 7; i++)
            {
                var digit = 0;
                for(int j = 0; j < i + 1; j++)
                {
                    digit = (digit + _grid[y][x]) % 5;
                    if(dir == 1 || dir == 2 || dir == 3)
                        x = (x + 1) % 10;
                    else if(dir == 5 || dir == 6 || dir == 7)
                        x = (x + 9) % 10;
                    if(dir == 7 || dir == 0 || dir == 1)
                        y = (y + 9) % 10;
                    else if(dir == 3 || dir == 4 || dir == 5)
                        y = (y + 1) % 10;
                }
                _info.BlackHoleCode.Add(digit);
                dir = (dir + (clockwise ? 7 : 1)) % 8;
            }

            int a = rnd.Next(0, 4);
            int b = rnd.Next(0, 2);

            Debug.LogFormat("<White Hole #{0}> Rule array indicies: {1} {2}", _moduleId, new[] { "right", "top", "left", "bottom" }[a], new[] { "clockwise", "counter-clockwise" }[b]);

            _info.SeedRule = new Func<float, float>[][] { new Func<float, float>[] { i => (360f - i) % 360f, i => i }, new Func<float, float>[] { i => (450f - i) % 360f, i => (i + 90f) % 360 }, new Func<float, float>[] { i => (540f - i) % 360f, i => (i + 180f) % 360 }, new Func<float, float>[] { i => (630f - i) % 360f, i => (i + 270f) % 360 } }[a][b];
        }
        if(_info.UnlinkedModules.Contains(this))
        {
            Debug.LogFormat(@"[White Hole #{0}] Unlinked modules Black Hole code = {1}", _moduleId, _info.SolutionCode.JoinString(" "));
            string code = "";
            for(int i = 0; i < _info.SolutionCode.Count; i++)
            {
                code += _info.SeedRule(table[(_info.StartDirection + (_info.Clockwise ? 1 : 7) * i) % 8][_info.SolutionCode[i]]).ToString() + " ";
            }
            Debug.LogFormat(@"[White Hole #{0}] Unlinked modules direction code = {1}", _moduleId, code);
        }
        else
        {
            string code = "";
            for(int i = 0; i < _info.BlackHoleCode.Count; i++)
            {
                code += _info.SeedRule(table[(_info.StartDirection + (_info.Clockwise ? 7 : 1) * i) % 8][_info.BlackHoleCode[i]]).ToString() + " ";
            }
            Debug.LogFormat("<White Hole #{0}> Generated Black Hole code = {1}", _moduleId, _info.BlackHoleCode.Join(" "));
            Debug.LogFormat("[White Hole #{0}] Linked modules direction code = {1}", _moduleId, code);
        }
    }

    private bool ObtainSwirl(GameObject swirl)
    {
        swirl.transform.parent = Selectable.transform;
        swirl.transform.localPosition = new Vector3(0f, 0f, 0f);
        swirl.transform.localEulerAngles = new Vector3(0f, 0f, 360f / 7f * swirlcount);

        StartCoroutine(SwirlOut(swirl, target));
        StartCoroutine(SwirlRotate(swirl, speed));

        swirlcount++;
        if(swirlcount >= 7)
        {
            speed = Rnd.Range(10f, 30f);
            swirlcount = 0;
            target = Rnd.Range(.99f, 1.05f);
            _digitsEntered++;
        }
        return false;
    }

    private IEnumerator SwirlRotate(GameObject swirl, float speed)
    {
        while(true)
        {
            swirl.transform.localEulerAngles -= new Vector3(0f, 0f, speed * Time.deltaTime);
            yield return null;
        }
    }

    private IEnumerator SwirlOut(GameObject swirl, float target)
    {
        float duration = 1f;
        float elapsed = 0f;
        float start = 0.9f;
        while(elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            float scale = Mathf.Lerp(start, target, elapsed);
            swirl.transform.localScale = new Vector3(scale, scale, scale);
        }
    }

    private bool ObtainNumber(GameObject number)
    {
        bool IsCheck;
        if(IsCheck = number.GetComponentInChildren<TextMesh>().color == Color.white)
            number.GetComponentInChildren<TextMesh>().color = Color.black;
        else
            _info.BlackHoleDigitsEntered++;

        if(_SuppressStrikes && !isCurrentAngleSet)
            Arrows[Mathf.RoundToInt((_info.SeedRule(table[(_info.StartDirection + (_info.Clockwise ? 7 : 1) * (_info.BlackHoleDigitsEntered - 1)) % 8][int.Parse(number.GetComponentInChildren<TextMesh>().text)]) - 22.5f) / 45f)].OnInteract();

        Debug.LogFormat("[White Hole #{0}] Obtained a{2} number: {1}", _moduleId, number.GetComponentInChildren<TextMesh>().text, IsCheck ? " (check)" : "");

        number.transform.parent = Selectable.transform;
        number.transform.localPosition = new Vector3(0f, 0f, 0f);
        number.transform.localEulerAngles = new Vector3(0f, 0f, Rnd.Range(0f, 360f));
        StartCoroutine(NumberOut(number, IsCheck));
        return false;
    }

    private IEnumerator NumberOut(GameObject number, bool IsCheck)
    {
        if(!IsCheck)
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Audio.PlaySoundAtTransform("BlackHoleBlow", Selectable.transform);
        const float outDuration = 1.5f;
        float outElapsed = 0f;
        float rotationSpeed = Rnd.Range(25f, 40f);
        if(!IsCheck && _info.UnlinkedModules.Contains(this)) { StartCoroutine(CreateSwirl(6 - wholeswirlcount)); wholeswirlcount++; }
        if(IsCheck || !isCurrentAngleSet)
        {
            if(!isCurrentAngleSet && !IsCheck)
            {
                Debug.LogFormat("[White Hole #{0}] You didn't choose a direction, so I struck.", _moduleId);
                if(!_SuppressStrikes)
                    Module.HandleStrike();
            }
            while(true)
            {
                outElapsed += Time.deltaTime;
                number.transform.localEulerAngles -= new Vector3(0f, 0f, rotationSpeed * Time.deltaTime);
                number.transform.GetChild(0).localEulerAngles -= new Vector3(0f, 0f, 2 * rotationSpeed * Time.deltaTime);
                if(outElapsed >= outDuration)
                {
                    Destroy(number);
                    yield break;
                }
                else
                {
                    var t = Mathf.Min(1, outElapsed / outDuration) * _planetSize;
                    number.transform.localScale = new Vector3(t, t, t);
                }
                yield return null;
            }
        }
        else
        {
            CheckDirection(int.Parse(number.GetComponentInChildren<TextMesh>().text), !_info.UnlinkedModules.Contains(this));
            while(true)
            {
                outElapsed += Time.deltaTime;
                number.transform.localEulerAngles -= new Vector3(0f, 0f, rotationSpeed * Time.deltaTime);
                number.transform.GetChild(0).localEulerAngles -= new Vector3(0f, 0f, 2 * rotationSpeed * Time.deltaTime);
                if(outElapsed >= outDuration)
                {
                    isCurrentAngleSet = false;
                    Destroy(number);
                    yield break;
                }
                else
                {
                    var t = Mathf.Min(1, outElapsed / outDuration) * _planetSize * 0.5f;
                    number.transform.localScale = new Vector3(t, t, t);
                    var u = Mathf.Min(1, outElapsed / outDuration);
                    number.transform.localPosition = new Vector3(Mathf.Cos(currentAngle / 180 * Mathf.PI) * u * 0.2f, Mathf.Sin(currentAngle / 180 * Mathf.PI) * u * 0.2f, 0f);
                }
                yield return null;
            }
        }
    }

    private static readonly float[][] table = new float[][] {
        new float[] { 22.5f, 337.5f, 292.5f, 337.5f, 67.5f },
        new float[] { 67.5f, 67.5f, 112.5f, 67.5f, 157.5f },
        new float[] { 337.5f, 202.5f, 67.5f, 247.5f, 22.5f },
        new float[] { 292.5f, 157.5f, 157.5f, 112.5f, 292.5f },
        new float[] { 202.5f, 292.5f, 247.5f, 157.5f, 337.5f },
        new float[] { 157.5f, 22.5f, 337.5f, 292.5f, 112.5f },
        new float[] { 247.5f, 112.5f, 202.5f, 22.5f, 202.5f },
        new float[] { 112.5f, 247.5f, 22.5f, 202.5f, 247.5f } };

    private void CheckDirection(int digit, bool isBlack)
    {
        if(isBlack)
        {
            if(currentAngle == _info.SeedRule(table[(_info.StartDirection + (_info.Clockwise ? 7 : 1) * (_info.BlackHoleDigitsEntered - 1)) % 8][digit]))
                Debug.LogFormat("[White Hole #{0}] That was the correct direction. Good job!", _moduleId);
            else
            {
                Debug.LogFormat("[White Hole #{0}] That wasn't correct. The correct direction was {1} degrees.", _moduleId, _info.SeedRule(table[(_info.StartDirection + (_info.Clockwise ? 7 : 1) * (_info.BlackHoleDigitsEntered - 1)) % 8][digit]));
                if(!_SuppressStrikes)
                    Module.HandleStrike();
            }
        }
        else
        {
            if(currentAngle == _info.SeedRule(table[(_info.StartDirection + (_info.Clockwise ? 1 : 7) * _info.DigitsEntered) % 8][digit]))
                Debug.LogFormat("[White Hole #{0}] That was the correct direction. Good job!", _moduleId);
            else
            {
                Debug.LogFormat("[White Hole #{0}] That wasn't correct. The correct direction was {1} degrees.", _moduleId, _info.SeedRule(table[(_info.StartDirection + (_info.Clockwise ? 1 : 7) * _info.DigitsEntered) % 8][digit]));
                if(!_SuppressStrikes)
                    Module.HandleStrike();
            }
        }
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} arrow 3 [specify which arrow to press, starting from the bottom, going counter-clockwise] | !{0} hole";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        if(_isSolved || _SuppressStrikes)
            yield break;

        var instructions = command.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(str => str.Trim().ToLowerInvariant()).ToArray();

        List<KMSelectable> queue = new List<KMSelectable>();

        for(int i = 0; i < instructions.Length; i++)
        {
            if(instructions[i] == "hole")
                queue.Add(Selectable);
            else if(instructions[i] == "arrow")
            {
                i++;
                if(i == instructions.Length)
                {
                    yield return "sendtochaterror Invalid arrow number: (none received)";
                    yield break;
                }
                if("1 2 3 4 5 6 7 8".Contains(instructions[i]))
                    queue.Add(Arrows[(int.Parse(instructions[i]) + 5) % 8]);
                else
                {
                    yield return "sendtochaterror Invalid arrow number: " + instructions[i];
                    yield break;
                }
            }
            else
            {
                yield return "sendtochaterror Invalid command: " + instructions[i];
                yield break;
            }
        }
        yield return null;

        yield return queue;
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        if(_isSolved || _SuppressStrikes)
            yield break;

        _SuppressStrikes = true;

        if(_info.UnlinkedModules.Contains(this))
        {
            if(isCurrentAngleSet)
                Selectable.OnInteract();

            while(_digitsEntered < _digitsExpected)
            {
                currentAngle = _info.SeedRule(table[(_info.StartDirection + (_info.Clockwise ? 1 : 7) * _info.DigitsEntered) % 8][_info.SolutionCode[_info.DigitsEntered]]);
                particles.transform.localEulerAngles = new Vector3(0f, 0f, currentAngle - 22.5f);
                particles.Play();
                Debug.LogFormat("[White Hole #{0}] You set the angle to {1} degrees.", _moduleId, currentAngle);
                isCurrentAngleSet = true;

                Selectable.OnInteract();
                foreach(var obj in WaitWithTrue(Rnd.Range(2.0f, 2.5f)))
                    yield return obj;
            }
        }
        else
        {
            yield return "sendtochat This module will solve immediately after its linked black hole solves. It will not strike, and no further inputs will be registered.";
            Debug.LogFormat("[White Hole #{0}] This module will solve immediately after its linked black hole solves. It will not strike, and no further inputs will be registered", _moduleId);
            while(_digitsEntered < _digitsExpected)
                yield return true;
            foreach(var obj in WaitWithTrue(0.5f))
                yield return obj;
            Selectable.OnInteract();
        }
    }

    IEnumerable WaitWithTrue(float time)
    {
        var startTime = Time.time;
        while(Time.time < startTime + time)
            yield return true;
    }
}