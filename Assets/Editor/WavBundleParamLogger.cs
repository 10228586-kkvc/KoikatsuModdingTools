/*
■AssetBundle BrowserのAsset Bundleに登録されているwavファイルの情報とログ出力する拡張スクリプト
Assets/Editor フォルダーを作成し、WavBundleParamLogger.csとして保存
実行: Unityエディタの上部メニューに AssetBundles > Log Wav AssetBundle Parameters が追加されるので、クリック
*/
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public class WavBundleParamLogger
{
	[MenuItem("AssetBundles/Log Wav AssetBundle Parameters")]
	public static void LogWavParameters()
	{
		// 1. プロジェクト内の全アセットバンドル名を取得
		string[] bundleNames = AssetDatabase.GetAllAssetBundleNames();

		if (bundleNames.Length == 0)
		{
			Debug.LogWarning("[WavLogger] アセットバンドルが1つも登録されていません。");
			return;
		}

		StringBuilder logBuilder = new StringBuilder();
		logBuilder.AppendLine("=== [Unity 5.6] AssetBundle .wav Parameter Inspection Log ===");

		int totalWavCount = 0;

		foreach (string bundleName in bundleNames)
		{
			// .unity3d バンドルのみ対象（または全バンドル）
			string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);

			bool bundleHasWav = false;

			foreach (string path in assetPaths)
			{
				if (!path.ToLower().EndsWith(".wav")) continue;

				if (!bundleHasWav)
				{
					logBuilder.AppendLine("\n--------------------------------------------------");
					logBuilder.AppendLine(string.Format("📦 AssetBundle Name: [{0}]", bundleName));
					logBuilder.AppendLine("--------------------------------------------------");
					bundleHasWav = true;
				}

				totalWavCount++;
				logBuilder.AppendLine(string.Format("\n📄 File: {0}", path));

				// AssetImporter (AudioImporter) のパラメータ取得
				AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;

				if (importer != null)
				{
					// 基本設定
					logBuilder.AppendLine(string.Format("   - Force To Mono: {0}", importer.forceToMono));
					logBuilder.AppendLine(string.Format("   - Preload Audio Data: {0}", importer.preloadAudioData));
					logBuilder.AppendLine(string.Format("   - Load In Background: {0}", importer.loadInBackground));

					// Unity 5.6 / デフォルト Sample Settings
					AudioImporterSampleSettings settings = importer.defaultSampleSettings;
					logBuilder.AppendLine(string.Format("   - Load Type: {0}", settings.loadType));
					logBuilder.AppendLine(string.Format("   - Compression Format: {0}", settings.compressionFormat));
					logBuilder.AppendLine(string.Format("   - Quality: {0}", settings.quality));
					logBuilder.AppendLine(string.Format("   - Sample Rate Setting: {0}", settings.sampleRateSetting));

					// アセットバンドル設定
					logBuilder.AppendLine(string.Format("   - Assigned Bundle Name: {0}", importer.assetBundleName));
					logBuilder.AppendLine(string.Format("   - Assigned Bundle Variant: {0}", importer.assetBundleVariant));
				}
				else
				{
					logBuilder.AppendLine("   ⚠️ AudioImporter の取得に失敗しました。");
				}
			}
		}

		logBuilder.AppendLine("\n==================================================");
		logBuilder.AppendLine(string.Format("Inspection Complete. Total WAV Files Logged: {0}", totalWavCount));
		logBuilder.AppendLine("==================================================");

		// コンソールに出力
		Debug.Log(logBuilder.ToString());

		// ログ量が多い場合のためにテキストファイルとしてもAssets直下に保存
		string logFilePath = Path.Combine(Application.dataPath, "../WavBundleParameters_Log.txt");
		File.WriteAllText(logFilePath, logBuilder.ToString());
		Debug.Log(string.Format("[WavLogger] ログファイルを保存しました: {0}", Path.GetFullPath(logFilePath)));
	}
}