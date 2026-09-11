/*
■フォルダを指定して、その中にあるwavファイルを一括でAssetBundle BrowserにAsset Bundleとして登録する拡張スクリプト
Assets/Editor フォルダーを作成し、AssetBundleBatchAssigner.csとして保存
実行: Unityエディタの上部メニューに AssetBundles > Assign Bundles and Variants が追加されるので、クリック
- 20260911: 
処理時間が非常に長くなっている最大の原因は、ループ内でファイルごとに audioImporter.SaveAndReimport() を個別に実行しているためです。このメソッドが呼ばれるたびに Unity が 1 ファイルずつ Vorbis 圧縮のエンコード処理（再インポート）を行うため、大量のファイルがある場合に膨大な時間がかかってしまいます。

これを劇的に高速化するには、処理中に Unity の自動インポート機能を停止（AssetDatabase.StartAssetEditing()）し、全設定が終わった後にまとめて一括反映する手法をとります。

あわせてご要望の「音声設定変更」「バンドル名設定」「アセットブラウザの自動更新」をすべて組み込んだ高速化版スクリプトを作成しました。
*/
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Reflection;
using System;

public class WavBundleCorrectAssigner
{
	[MenuItem("AssetBundles/Configure Wav Bundles (Fast)")]
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

		// ★高速化の重要ポイント1: 自動インポートとGUI更新をブロック
		AssetDatabase.StartAssetEditing();

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

				// 末尾の ".unity3d" を取り除いて Bundle Name とし、Variant に "unity3d" を指定
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

						// 3. アセットバンドル名とバリアントの設定
						if (audioImporter.assetBundleName != targetBundleName || audioImporter.assetBundleVariant != targetVariant)
						{
							audioImporter.SetAssetBundleNameAndVariant(targetBundleName, targetVariant);
							isModified = true;
						}

						// ★高速化の重要ポイント2: SaveAndReimport() を廃止し、メタデータの保存のみにする
						if (isModified)
						{
							EditorUtility.SetDirty(audioImporter);
							assignedCount++;
						}
					}
				}
			}
		}
		finally
		{
			// ★高速化の重要ポイント3: ブロックを解除して一括処理・インポートを適用
			AssetDatabase.StopAssetEditing();
			AssetDatabase.RemoveUnusedAssetBundleNames();
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		// 4. AssetBundle Browser（アセットブラウザ）の表示を自動更新
		RefreshAssetBundleBrowser();

		Debug.Log(string.Format("完了: {0} 個の .wav ファイルの設定更新とバンドル登録を高速処理しました！", assignedCount));
		EditorUtility.DisplayDialog("完了", string.Format("{0} 個の .wav ファイルの登録と設定更新が完了しました！\nアセットブラウザの表示も更新されました。", assignedCount), "OK");
	}

	/// <summary>
	/// 開いている AssetBundle Browser ウィンドウの表示を自動更新する
	/// </summary>
	private static void RefreshAssetBundleBrowser()
	{
		var browserTypes = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany(a => {
				try { return a.GetTypes(); }
				catch { return new Type[0]; }
			})
			.Where(t => t.Name == "AssetBundleBrowser" || t.Name == "AssetBundleManageTab")
			.ToArray();

		foreach (var type in browserTypes)
		{
			UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(type);
			foreach (var win in windows)
			{
				EditorWindow window = win as EditorWindow;
				if (window != null)
				{
					MethodInfo forceReloadMethod = type.GetMethod("ForceReloadData", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
					if (forceReloadMethod != null)
					{
						forceReloadMethod.Invoke(window, null);
					}
					window.Repaint();
				}
			}
		}
	}
}