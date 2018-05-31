using UnityEngine;
using System.Collections.Generic;
using System.Linq;

using Edgework;
using System.Text.RegularExpressions;

public class EdgeworkModule : MonoBehaviour
{
	public KMBombInfo BombInfo;
	public KMNeedyModule BombModule;
	public KMAudio KMAudio;

	public KMSelectable[] Buttons;
	public TextMesh[] Labels;
	public TextMesh DisplayText;

	private string[] QuestionTemplates = new string[]{
		"What is the\nstate of the\n{0} indicator?",
		"How many\n{0}\nare there?",
		"Is there a\n{0}\npresent?",
		"What is the\n{0} character\nof the S.N.?"
	};

	private Dictionary<string, int> Totals;
	private Dictionary<string, int> Ports;
	private Dictionary<string, int> Batteries;
	private Dictionary<string, int> Indicators;

	private string[] TotalNames = new string[] {"Ports", "Batteries", "Battery Holders", "Indicators", "Unlit Indicators", "Lit Indicators"};
	private string[] PortNames = new string[] {"Serial", "Parallel", "DVI-D", "RJ-45", "PS2", "Stereo RCA"};
	private string[] BatteryNames = new string[] {"AA", "D"};
	private string[] IndicatorNames = new string[] {"SND", "CLR", "CAR", "IND", "FRQ", "SIG", "NSA", "MSA", "TRN", "BOB", "FRK"};
	private string[] NumNames = new string[] {"1st", "2nd", "3rd", "4th", "5th", "6th"};

	private Color Yellow = new Color(1,1,0,1);
	private Color Green = Color.green;
	private Color Red = Color.red;

	int CurrentQuestion = 0;
	int MaxQuestions = 3;
	private Question[] Questions;
	bool canPressButtons = false;
	private List<string> UsedQuestions = new List<string>();
    bool strikeOccurred = false;

    int moduleId;
    static int moduleIdCounter = 1;

    private class Question {
		private string QuestionTemplate;
		private string QuestionData;
		public string[] Buttons;
		public int CorrectButton;

		public Question(string template, string data, string button0, string button1, string button2, int answer) {
			QuestionTemplate = template;
			QuestionData = data;
			Buttons = new string[] {button0, button1, button2};
			CorrectButton = answer;
		}

		public string GetQuestionText() {
			return System.String.Format(QuestionTemplate, QuestionData);
		}
	}

	void Start() {
        moduleId = moduleIdCounter++;
        Totals = new Dictionary<string, int>();

		Ports = new Dictionary<string, int>();
		Ports.Add("Serial", BombInfo.GetPortCount(KMBombInfoExtensions.KnownPortType.Serial));
		Ports.Add("Parallel", BombInfo.GetPortCount(KMBombInfoExtensions.KnownPortType.Parallel));
		Ports.Add("DVI-D", BombInfo.GetPortCount(KMBombInfoExtensions.KnownPortType.DVI));
		Ports.Add("RJ-45", BombInfo.GetPortCount(KMBombInfoExtensions.KnownPortType.RJ45));
		Ports.Add("PS2", BombInfo.GetPortCount(KMBombInfoExtensions.KnownPortType.PS2));
		Ports.Add("Stereo RCA", BombInfo.GetPortCount(KMBombInfoExtensions.KnownPortType.StereoRCA));
		Totals.Add("Ports", BombInfo.GetPortCount());

		Batteries = new Dictionary<string, int>();
		Batteries.Add("AA", BombInfo.GetBatteryCount(KMBombInfoExtensions.KnownBatteryType.AA));
		Batteries.Add("D", BombInfo.GetBatteryCount(KMBombInfoExtensions.KnownBatteryType.D));
		Totals.Add("Batteries", BombInfo.GetBatteryCount());
		Totals.Add("Battery Holders", BombInfo.GetBatteryHolderCount());

		Indicators = new Dictionary<string, int>();
		foreach (string label in System.Enum.GetNames(typeof(KMBombInfoExtensions.KnownIndicatorLabel))) {
			Indicators.Add(label, IndicatorNum(label));
		}
		Totals.Add("Indicators", Enumerable.Count(BombInfo.GetIndicators()));
		Totals.Add("Unlit Indicators", Enumerable.Count(BombInfo.GetOffIndicators()));
		Totals.Add("Lit Indicators", Enumerable.Count(BombInfo.GetOnIndicators()));
	}

	int IndicatorNum(string label) {
		if (!BombInfo.IsIndicatorPresent(label)) {
			return 0;
		} else if (BombInfo.IsIndicatorOff(label)) {
			return 1;
		} else {
			return 2;
		}
	}

	void Awake() {
		GetComponent<KMNeedyModule>().OnNeedyActivation += OnNeedyActivation;
		GetComponent<KMNeedyModule>().OnNeedyDeactivation += OnNeedyDeactivation;
		GetComponent<KMNeedyModule>().OnTimerExpired += OnTimerExpired;
		for (int i = 0; i < 3; i++) {
			var j = i;
			Buttons[i].OnInteract += delegate { HandlePress(j); return false; };
		}
		ClearDisplays();
	}

	void OnNeedyActivation() {
		CurrentQuestion = 0;
        strikeOccurred = false;
		Questions = new Question[MaxQuestions];
        Debug.LogFormat("[Edgework #{0}] Module Activated with {1} questions.", moduleId, MaxQuestions);
        for (int i = 0; i < MaxQuestions; i++) {
			Questions[i] = GenerateQuestion();
            Debug.LogFormat("[Edgework #{0}] Question {1}: \"{2}\". Answer: \"{3}\". Buttons: [\"{4}\", \"{5}\", \"{6}\"]", moduleId, i + 1, Regex.Replace(Questions[i].GetQuestionText(), @"\t|\n|\r", " "), Questions[i].Buttons[Questions[i].CorrectButton], Questions[i].Buttons[0], Questions[i].Buttons[1], Questions[i].Buttons[2]);
        }
		ResetDisplays();
	}

	void OnNeedyDeactivation() {
        Debug.LogFormat("[Edgework #{0}] Module Deactivated on question {1}.", moduleId, CurrentQuestion + 1);
        CurrentQuestion = 0;
		ClearDisplays();
        strikeOccurred = false;
	}

	void OnTimerExpired() {
        if (!strikeOccurred) BombModule.HandleStrike();
        strikeOccurred = false;
		DisplayIncorrect();
		DisplayText.text = "Time Ran Out!";
        Debug.LogFormat("[Edgework #{0}] Time ran out at question {1}", moduleId, CurrentQuestion + 1);
        Invoke("ClearDisplays", 2);
	}
		
	void HandlePress(int button) {
		KMAudio.PlaySoundAtTransform("tick", this.transform);
		Buttons[button].AddInteractionPunch();
        if (!canPressButtons) {
        } else if (canPressButtons && button == Questions[CurrentQuestion].CorrectButton) {
            CurrentQuestion++;
            DisplayCorrect();
            if (CurrentQuestion >= MaxQuestions) {
                BombModule.HandlePass();
                Debug.LogFormat("[Edgework #{0}] Button {1} pressed correctly! Module Passed.", moduleId, button + 1);
                Invoke("ClearDisplays", 0.75f);
            } else {
                Debug.LogFormat("[Edgework #{0}] Button {1} pressed correctly!", moduleId, button + 1);
                Invoke("ResetDisplays", 0.75f);
            }
        } else if (canPressButtons && button == 1 && Questions[CurrentQuestion].Buttons[1] == "") {
        } else if (canPressButtons) {
            BombModule.HandleStrike();
            strikeOccurred = true;
            DisplayIncorrect();
            canPressButtons = false;
            Debug.LogFormat("[Edgework #{0}] Button {1} pressed incorrectly!", moduleId, button + 1);
            BombModule.HandlePass();
            Invoke("ClearDisplays", 1.25f);
        } else {
            Debug.LogFormat("[Edgework #{0}] ERROR!", moduleId, button + 1);
            BombModule.HandleStrike();
            strikeOccurred = true;
            BombModule.HandlePass();
        }
    }

	void DisplayCorrect() {
		canPressButtons = false;
		DisplayText.text = "Correct!";
		DisplayText.color = Green;
		for (int i = 0; i < 3; i++) {
			Labels[i].text = "";
		}
	}

	void DisplayIncorrect() {
		canPressButtons = false;
		DisplayText.text = "Incorrect!";
		DisplayText.color = Red;
		for (int i = 0; i < 3; i++) {
			Labels[i].text = "";
		}
	}

	void ClearDisplays() {
		DisplayText.text = "";
		DisplayText.color = Yellow;
		for (int i = 0; i < 3; i++) {
			Labels[i].text = "";
			Labels[i].color = Yellow;
		}
	}

	void ResetDisplays() {
		DisplayText.text = Questions[CurrentQuestion].GetQuestionText();
		DisplayText.color = Yellow;
		for (int i = 0; i < 3; i++) {
			Labels[i].text = Questions[CurrentQuestion].Buttons[i];
			Labels[i].color = Yellow;
		}
		canPressButtons = true;
	}

	Question GenerateQuestion() {
		int questionType = 0;
		int type = Random.Range(0, 3);
        string res = null;
		int n = 0;
		string suffix = "";
		string[] b = new string[3];
		int r = -1;
        int tries = 0;

		FYShuffle(TotalNames);
		FYShuffle(PortNames);
		FYShuffle(BatteryNames);
		FYShuffle(IndicatorNames);

		for (int i = 0; i < 20; i++) {
			questionType = Random.Range(0,4);
			switch (questionType) {
				case 0: // Indicator State?
					if (Totals["Indicators"] == 0) continue;
					for (int j = 0; j < Indicators.Count; j++) {
                        tries++;
                        if (UsedQuestions.Contains("0 " + IndicatorNames[j]) || Indicators[IndicatorNames[j]] == 0) continue;
						res = IndicatorNames[j];
						break;
					}
					if (res == null) break; 
					UsedQuestions.Add("0 " + res);
					return new Question(QuestionTemplates[0], res, "Off", "", "On", (Indicators[res]-1)*2);

				case 1: // How Many?
                    switch (type) {
						case 0: // Totals
							for (int j = 0; j < Totals.Count; j++) {
                                tries++;
                                if (UsedQuestions.Contains("1 TOTALS " + TotalNames[j]) || Totals[TotalNames[j]] == 0) continue;
								res = TotalNames[j];
								break;
							}
							if (res == null) break;
							UsedQuestions.Add("1 TOTALS " + res);
							n = Totals[res];
							suffix = "";
							break;
						case 1: // Ports
							for (int j = 0; j < Ports.Count; j++) {
                                tries++;
                                if (UsedQuestions.Contains("1 PORTS " + PortNames[j]) || Ports[PortNames[j]] == 0) continue;
								res = PortNames[j];
								break;
							}
							if (res == null) break;
							UsedQuestions.Add("1 PORTS " + res);
							n = Ports[res];
							suffix = " ports";
							break;
						case 2: // Batteries
							for (int j = 0; j < Batteries.Count; j++) {
                                tries++;
                                if (UsedQuestions.Contains("1 BATTERIES " + BatteryNames[j]) || Batteries[BatteryNames[j]] == 0) continue;
								res = BatteryNames[j];
								break;
							}
							if (res == null) break;
							UsedQuestions.Add("1 BATTERIES " + res);
							n = Batteries[res];
							suffix = " batteries";
							break;
						default:
							res = null;
							break;
					}
					if (res == null) break;
					b[0] = "" + n;
					for (int k = 1; k < 3;) {
                        tries++;
                        r = Random.Range(Mathf.Max(0, n - 2), n + 3);
						if(!b.Contains("" + r)) {
							b[k++] = "" + r;
						}
					}
                    FYShuffle(b);
					return new Question(QuestionTemplates[1], res + suffix, b[0], b[1], b[2], System.Array.IndexOf(b, ""+n));

				case 2: // Is Present?
                    switch (type) {
						case 0: // Ports
							for (int j = 0; j < Ports.Count; j++) {
                                tries++;
                                if (UsedQuestions.Contains("2 PORTS " + PortNames[j])) continue;
								res = PortNames[j];
								break;
							}
							if (res == null) break;
							UsedQuestions.Add("2 PORTS " + res);
							n = Ports[res];
							suffix = " port";
							break;

						case 1: // Batteries
							for (int j = 0; j < Batteries.Count; j++) {
                                tries++;
                                if (UsedQuestions.Contains("2 BATTERIES " + BatteryNames[j])) continue;
								res = BatteryNames[j];
								break;
							}
							if (res == null) break;
							UsedQuestions.Add("2 BATTERIES " + res);
							n = Batteries[res];
							suffix = " battery";
							break;

						case 2: // Indicator
							for (int j = 0; j < Batteries.Count; j++) {
                                tries++;
                                if (UsedQuestions.Contains("2 INDICATORS " + IndicatorNames[j])) continue;
								res = IndicatorNames[j];
								break;
							}
							if (res == null) break;
							UsedQuestions.Add("2 INDICATORS " + res);
							n = Indicators[res];
							suffix = " indicator";
							break;

						default:
							res = null;
							break;
					}
					if (res == null) break;
					int answer;
					if (n > 0) {
						answer = 2;
					} else {
						answer = 0;
					}
					return new Question(QuestionTemplates[2], res + suffix, "No", "", "Yes", answer);

				case 3: // Serial Number
                    int z = -1;
					for (int j = 0; j < 6; j++) {
                        tries++;
                        z = Random.Range(0,6);
						if (UsedQuestions.Contains("3 " + BombInfo.GetSerialNumber()[z])) continue;
						res = "" + BombInfo.GetSerialNumber()[z];
						break;
					}
					if (res == null) break;
					UsedQuestions.Add("3 " + res);
					b = new string[3];
					b[0] = res;
					for (int k = 1, t = 1; k < 3 && t < 20; t++) {
                        tries++;
                        r = Random.Range(Mathf.Max(0, z - 2), Mathf.Min(z + 3, 6));
						if(!b.Contains("" + BombInfo.GetSerialNumber()[r])) {
							b[k++] = "" + BombInfo.GetSerialNumber()[r];
						}
					}
                    if (b[0] == null) break;
                    FYShuffle(b);
					return new Question(QuestionTemplates[3], NumNames[z], b[0], b[1], b[2], System.Array.IndexOf(b, res));
			}
		}
        Debug.LogFormat("[Edgework #{0}] Used question list cleared after {1} tries!", moduleId, tries);
        UsedQuestions.Clear();
		return GenerateQuestion();
	}

	void FYShuffle(string[] array) {
		for (int i = 0; i < array.Length; i++) {
			string temp = array[i];
			int ri = Random.Range(i, array.Length);
			array[i] = array[ri];
			array[ri] = temp;
		}
	}
}