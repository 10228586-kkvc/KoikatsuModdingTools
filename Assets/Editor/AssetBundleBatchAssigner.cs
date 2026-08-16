/*
Assets/Editor フォルダーを作成し、AssetBundleBatchAssigner.csとして保存
実行: Unityエディタの上部メニューに AssetBundles > Assign Bundles and Variants が追加されるので、クリック
*/
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class WavBundleAutoAssigner
{
    [MenuItem("AssetBundles/Configure Wav Bundles (Select Folder)")]
    public static void ConfigureWavBundles()
    {
        // 1. フォルダ選択ダイアログを開く (デフォルトは Assets フォルダ)
        string selectedPath = EditorUtility.OpenFolderPanel("Wavファイルを検索するフォルダを選択", "Assets", "");

        // キャンセルされた場合は処理を中断
        if (string.IsNullOrEmpty(selectedPath)) return;

        // パス区切りをスラッシュに統一
        selectedPath = selectedPath.Replace("\\", "/");

        // プロジェクトの Assets フォルダの絶対パスを取得
        string projectAssetsPath = Application.dataPath.Replace("\\", "/");

        // 選択されたフォルダが Assets フォルダ配下にあるか確認
        if (!selectedPath.StartsWith(projectAssetsPath))
        {
            EditorUtility.DisplayDialog("エラー", "プロジェクト内の「Assets」フォルダ以下のフォルダを選択してください。", "OK");
            return;
        }

        // 2. 絶対パスを Unity のアセットパス ("Assets/..." 形式) に変換
        string relativeRootPath = "Assets" + selectedPath.Substring(projectAssetsPath.Length);

        // 3. 削除対象となるプレフィックス（Assets から選択フォルダまでの部分）を計算
        string prefixToRemove = relativeRootPath.ToLower() + "/";

        AssetDatabase.StartAssetEditing();

        try
        {
            // 選択フォルダ以下の全サブフォルダを取得
            string[] allDirectories = Directory.GetDirectories(relativeRootPath, "*", SearchOption.AllDirectories)
                                                .Select(d => d.Replace("\\", "/"))
                                                .ToArray();

            var directoryList = allDirectories.ToList();
            directoryList.Insert(0, relativeRootPath);

            int assignedCount = 0;

            foreach (string currentDir in directoryList)
            {
                // 直下の .wav ファイルを取得
                string[] wavFiles = Directory.GetFiles(currentDir, "*.wav", SearchOption.TopDirectoryOnly);
                if (wavFiles.Length == 0) continue;

                // 4. パスの変換処理
                string lowerCurrentDir = currentDir.ToLower() + "/";
                string bundlePath;

                if (lowerCurrentDir.StartsWith(prefixToRemove))
                {
                    // 選択されたフォルダまでのパス（Assets/...）を削除する
                    bundlePath = lowerCurrentDir.Substring(prefixToRemove.Length).TrimEnd('/');
                }
                else
                {
                    // 選択フォルダ自体に wav が直置きされていた場合
                    bundlePath = Path.GetFileName(currentDir).ToLower();
                }

                // 末尾に .unity3d を結合
                string finalBundleName = bundlePath + ".unity3d";

                // .wav ファイルにバンドル名を登録
                foreach (string wavPath in wavFiles)
                {
                    string assetPath = wavPath.Replace("\\", "/");
                    AssetImporter importer = AssetImporter.GetAtPath(assetPath);

                    if (importer != null)
                    {
                        importer.assetBundleName = finalBundleName;
                        importer.assetBundleVariant = "";
                        assignedCount++;
                    }
                }
            }

            // string.Format に書き換えて C# 4.0 に対応
            Debug.Log(string.Format("完了: {0} 個の wav ファイルに AssetBundle 名を割り当てました。", assignedCount));
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();
    }
}