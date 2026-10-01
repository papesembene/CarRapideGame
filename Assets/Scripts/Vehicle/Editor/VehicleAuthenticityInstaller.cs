#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CarRapide.EditorTools
{
    public static class VehicleAuthenticityInstaller
    {
        const string Folder="Assets/Resources/CarRapide/Characters/Workwear";
        [MenuItem("Car Rapide/Vehicle/Apply reference workwear and open cab windows")]
        public static void Install()
        {
            if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
            foreach(string name in new[]{"DriverWorkwear","ReceiverWorkwear"})
            {
                string path=Folder+"/"+name+".png";
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.maxTextureSize=2048; importer.textureCompression=TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
                string materialPath=Folder+"/"+name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!material) {material=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,materialPath);}
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                material.SetFloat("_Smoothness",.12f); EditorUtility.SetDirty(material);
            }
            var experience=Object.FindAnyObjectByType<CarRapide.Vehicle.VehicleDriverExperience>();
            foreach(var filter in experience.transform.Find("Car rapide").GetComponentsInChildren<MeshFilter>())
            {
                if(filter.name!="Porte_avant_gauche" && filter.name!="Porte_avant_droit") continue;
                var source=PrefabUtility.GetCorrespondingObjectFromSource(filter).sharedMesh;
                string path="Assets/Art/Vehicles/CarRapide/"+filter.name+"_OpenWindow.asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(!mesh) {mesh=Object.Instantiate(source); AssetDatabase.CreateAsset(mesh,path);}
                else EditorUtility.CopySerialized(source,mesh);
                mesh.name=filter.name+"_OpenWindow";
                // The FBX reverses the material order between the left and right doors.
                var materials=filter.GetComponent<Renderer>().sharedMaterials;
                for(int sub=0;sub<materials.Length;sub++)
                    if(materials[sub].name=="CabinGlass" || materials[sub].name=="Parebrise")
                        mesh.SetTriangles(System.Array.Empty<int>(),sub);
                mesh.RecalculateBounds(); filter.sharedMesh=mesh;
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                EditorUtility.SetDirty(mesh);
            }
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(experience.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(experience.gameObject.scene);
        }
    }
}
#endif
