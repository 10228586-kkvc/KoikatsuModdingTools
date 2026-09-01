/*
■AssetBundle BrowserのAsset Bundleに登録されたファイルを一括解除する拡張スクリプト
Assets/Editor フォルダーを作成し、ClearAllAssetBundleNames.csとして保存
実行: Unityエディタの上部メニューに AssetBundles > Clear All AssetBundle Names が追加されるので、クリック
*/
using UnityEngine;
using UnityEditor;

public class ClearAllAssetBundleNames
{
	[MenuItem("AssetBundles/Clear All AssetBundle Names")]
	public static void ClearAllNames()
	{
		// プロジェクト内のすべての未割り当て含むバンドル名を取得
		string[] bundleNames = AssetDatabase.GetAllAssetBundleNames();
		int count = 0;

		foreach (string bundleName in bundleNames)
		{
			// 強制的にバンドル名を削除（True指定で内部の参照アセットからも全除去）
			AssetDatabase.RemoveAssetBundleName(bundleName, true);
			count++;
		}

		// データベースとプロジェクト設定をディスクに強制同期
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();

		Debug.Log(string.Format("リセット完了: {0} 個の AssetBundle 名を完全に削除・消去しました。", count));
	}
}