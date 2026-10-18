
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// EDT-03
namespace ZeroShotUdonUI.EditorTools
{
    /// <summary>
    /// 設定チェック（SceneChecker）の結果を一覧で表示する画面。
    /// 行を押すと対象のオブジェクトが選ばれ、自動で直せる問題には「直す」ボタンが付く。
    /// </summary>
    public class CheckWindow : EditorWindow
    {
        private List<CheckIssue> issues = new List<CheckIssue>();
        private Vector2 scrollPosition;

        [MenuItem("Tools/ZeroShot Udon UI/設定チェック")]
        private static void Open()
        {
            CheckWindow window = GetWindow<CheckWindow>(false, "設定チェック");
            window.minSize = new Vector2(480f, 360f);
            window.Show();
        }

        private void OnEnable()
        {
            // Hierarchy でオブジェクトを足したり消したりしたときに、一覧を新しくする。
            EditorApplication.hierarchyChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= Refresh;
        }

        // ほかの画面で直してから戻ってきたときに、結果を新しくする。
        private void OnFocus()
        {
            Refresh();
        }

        private void Refresh()
        {
            issues = SceneChecker.Check();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "開いているシーンを調べ、VRChat で操作できない原因や、意図どおりに動かない原因になる設定を探します。\n"
                + "ツールキットの部品を含む Canvas だけが対象です。", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("もう一度調べる"))
                    Refresh();

                if (GUILayout.Button("Console に書き出す"))
                    SceneChecker.Log(issues);

                using (new EditorGUI.DisabledScope(!HasFixable()))
                {
                    if (GUILayout.Button("直せるものをすべて直す"))
                        FixAll();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(GetSummary(), EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            foreach (CheckIssue issue in issues)
            {
                // 「直す」を押すと一覧を作り直すので、その回の描画は打ち切る。
                if (DrawIssue(issue))
                    break;
            }
            EditorGUILayout.EndScrollView();
        }

        // 問題を1件描く。「直す」が押されて一覧を作り直した場合は true を返す。
        private bool DrawIssue(CheckIssue issue)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    SceneChecker.GetSeverityLabel(issue.Severity) + "：" + issue.Message, EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("直し方：" + issue.Advice, EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(issue.Target == null))
                    {
                        if (GUILayout.Button(new GUIContent(SceneChecker.GetTargetPath(issue.Target), "押すと、このオブジェクトを選ぶ"), EditorStyles.linkLabel))
                        {
                            Selection.activeObject = issue.Target;
                            EditorGUIUtility.PingObject(issue.Target);
                        }
                    }

                    GUILayout.FlexibleSpace();

                    if (issue.CanFix && GUILayout.Button("直す", GUILayout.Width(64f)))
                    {
                        issue.Fix();
                        Refresh();
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasFixable()
        {
            foreach (CheckIssue issue in issues)
            {
                if (issue.CanFix)
                    return true;
            }

            return false;
        }

        // 1つ直すと、ほかの問題の状況が変わることがある（例：当たり判定を足すと、大きさの警告も消える）。
        // そのため、1つ直すたびに調べ直す。
        private void FixAll()
        {
            const int MaxRounds = 100;

            for (int round = 0; round < MaxRounds; round++)
            {
                CheckIssue fixable = null;
                foreach (CheckIssue issue in issues)
                {
                    if (issue.CanFix)
                    {
                        fixable = issue;
                        break;
                    }
                }

                if (fixable == null)
                    break;

                fixable.Fix();
                issues = SceneChecker.Check();
            }

            Repaint();
        }

        private string GetSummary()
        {
            if (issues.Count == 0)
                return "問題は見つかりませんでした。";

            int errors = 0;
            int warnings = 0;
            int infos = 0;
            foreach (CheckIssue issue in issues)
            {
                switch (issue.Severity)
                {
                    case CheckSeverity.Error: errors++; break;
                    case CheckSeverity.Warning: warnings++; break;
                    default: infos++; break;
                }
            }

            return "エラー " + errors + " 件、警告 " + warnings + " 件、情報 " + infos + " 件";
        }
    }
}
