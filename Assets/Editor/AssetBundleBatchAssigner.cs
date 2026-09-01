/*
■フォルダを指定して、その中にあるwavファイルを一括でAssetBundle BrowserにAsset Bundleとして登録する拡張スクリプト
Assets/Editor フォルダーを作成し、AssetBundleBatchAssigner.csとして保存
実行: Unityエディタの上部メニューに AssetBundles > Assign Bundles and Variants が追加されるので、クリック
*/
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class WavBundleCorrectAssigner
{
	[MenuItem("AssetBundles/Configure Wav Bundles (Fix Variant Style)")]
	public static void ConfigureWavBundles()
	{
		// 1. ルートフォルダ（Assets以下）の選択
		string selectedPath = EditorUtility.OpenFolderPanel("Wavが含まれるルートフォルダを選択", "Assets", "");
		if (string.IsNullOrEmpty(selectedPath)) return;

		selectedPath = selectedPath.Replace("\\", "/");
		string projectAssetsPath = Application.dataPath.Replace("\\", "/");

		if (!selectedPath.StartsWith(projectAssetsPath))
		{
			EditorUtility.DisplayDialog("エラー", "プロジェクト内の「Assets」フォルダ以下のフォルダを選択してください。", "OK");
			return;
		}

		string relativeRootPath = "Assets" + selectedPath.Substring(projectAssetsPath.Length);
		string prefixToRemove = (relativeRootPath + "/").ToLower();

		string[] allDirectories = Directory.GetDirectories(relativeRootPath, "*", SearchOption.AllDirectories)
											.Select(d => d.Replace("\\", "/"))
											.ToArray();

		var directoryList = allDirectories.ToList();
		directoryList.Insert(0, relativeRootPath);

		int assignedCount = 0;

		try
		{
			foreach (string currentDir in directoryList)
			{
				// .wav ファイルを取得
				string[] wavFiles = Directory.GetFiles(currentDir, "*.wav", SearchOption.TopDirectoryOnly)
											 .Where(f => !Path.GetFileName(f).StartsWith("."))
											 .ToArray();

				if (wavFiles.Length == 0) continue;

				string lowerCurrentDir = currentDir.ToLower();
				string relativePath = lowerCurrentDir;

				if (lowerCurrentDir.StartsWith(prefixToRemove))
				{
					relativePath = lowerCurrentDir.Substring(prefixToRemove.Length);
				}

				if (string.IsNullOrEmpty(relativePath)) continue;

				// ★最大の修正ポイント★
				// 末尾の ".unity3d" を取り除いて Bundle Name とし、
				// Variant に "unity3d" を指定する（手動設定と完全に同一にする）
				string targetBundleName = relativePath.EndsWith(".unity3d") 
					? relativePath.Substring(0, relativePath.Length - 8) 
					: relativePath;

				string targetVariant = "unity3d";

				foreach (string wavPath in wavFiles)
				{
					string assetPath = wavPath.Replace("\\", "/");
					AudioImporter audioImporter = AssetImporter.GetAtPath(assetPath) as AudioImporter;

					if (audioImporter != null)
					{
						bool isModified = false;

						// 1. Preload Audio Data
						if (!audioImporter.preloadAudioData)
						{
							audioImporter.preloadAudioData = true;
							isModified = true;
						}

						// 2. Sample Settings (Vorbis / DecompressOnLoad)
						AudioImporterSampleSettings settings = audioImporter.defaultSampleSettings;
						if (settings.loadType != AudioClipLoadType.DecompressOnLoad)
						{
							settings.loadType = AudioClipLoadType.DecompressOnLoad;
							isModified = true;
						}
						if (settings.compressionFormat != AudioCompressionFormat.Vorbis || settings.quality < 0.99f)
						{
							settings.compressionFormat = AudioCompressionFormat.Vorbis;
							settings.quality = 1.0f;
							isModified = true;
						}

						if (isModified)
						{
							audioImporter.defaultSampleSettings = settings;
						}

						// 3. アセットバンドル名とバリアントの設定（手動登録スタイルを再現）
						if (audioImporter.assetBundleName != targetBundleName || audioImporter.assetBundleVariant != targetVariant)
						{
							// Name と Variant を分けて登録
							audioImporter.SetAssetBundleNameAndVariant(targetBundleName, targetVariant);
							isModified = true;
						}

						// 変更を確定
						if (isModified)
						{
							audioImporter.SaveAndReimport();
							assignedCount++;
						}
					}
				}
			}
		}
		finally
		{
			AssetDatabase.RemoveUnusedAssetBundleNames();
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		Debug.Log(string.Format("完了: {0} 個の .wav ファイルを手動登録（Name + Variantスタイル）と完全同一の設定で更新しました！", assignedCount));
	}
}