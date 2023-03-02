using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Esri.ArcGISMapsSDK.Components;
using Unity.Mathematics;
using Esri.HPFramework;
using Esri.GameEngine.Geometry;
using Newtonsoft.Json.Linq;

public class Minimap : MonoBehaviour
{

    /* origin coords for testing
     * 
     * -72.9108982
     * 41.3260946
     * 10
     */

    public ArcGISMapComponent map; //to get origin coordinates
    /* Adjust for demo purposes */
    public int scale = 500;

    public List<double3> locations; //starts with locs from FS
    [SerializeField] List<GameObject> markers = new List<GameObject>(); //starts empty

    public GameObject markerPrefab;

    public string _viewpointServiceURL = "https://services1.arcgis.com/wQnFk5ouCfPzTlPw/arcgis/rest/services/NewHaven_Viewpoints/FeatureServer/0";

    void Start()
    {
        //locations.Add(new double3(-72.9108982, 41.3260946, 10));
        //locations.Add(new double3(-72.9124226410097, 41.3254348359225, 10));
        //locations.Add(new double3(-72.9091276205202, 41.3259003143511, 20));
        ReadFromFS(); 

        CreateMinimap();
    }

    #region Math

    Vector3 WorldToMinimap(double3 loc)
    {
        /*
         * Translation matrix - treats the lat/lon/alt position of map origin as (0,0,0),
         * will transform other locations into that local space
         * double4x4 object necessary for the ArcGISPoint properties
         */
        double4x4 DMinimapMatrix = new double4x4
            (1, 0, 0, (map.OriginPosition.Y * scale * -1),
            0, 1, 0, (map.OriginPosition.Z * -0.1 ),
            0, 0, 1, (map.OriginPosition.X * scale * -1),
            0, 0, 0, 1);
        Matrix4x4 MinimapMatrix = DMinimapMatrix.ToMatrix4x4();

        double3 DViewpointPos = new double3(loc.y * scale, loc.z * 0.1, loc.x * scale);
        Vector3 ViewpointPos = DViewpointPos.ToVector3();

        /*
         * Returns scaled position relative to map origin
         */
        Vector3 point = MinimapMatrix.MultiplyPoint3x4(ViewpointPos);

        Matrix4x4 reflectionMatrix = new Matrix4x4();
        reflectionMatrix = Matrix4x4.identity;
        reflectionMatrix.SetColumn(0, new Vector4(-1, 0, 0, 0));
        // Without this the point will be reflected over the X axis
        return reflectionMatrix.MultiplyPoint3x4(point);
    }

    #endregion

    #region Minimap Methods
    public void CreateMinimap()
    {
        for(int i = 0; i < locations.Count; i+= 1)
        {
            AddMarker(i, false);
        }
    }



    public void AddMarker(int index, bool addedByUser)
    {
        double3 loc = locations[index];
        Vector3 markerPos = WorldToMinimap(loc);
        Debug.Log(markerPos);
        GameObject newMarker = Instantiate(markerPrefab, this.transform);

        newMarker.transform.localPosition = markerPos;
        newMarker.transform.Rotate(new Vector3(0, 1, 0), 180);
        newMarker.GetComponent<MinimapMarker>().addedByUser = addedByUser;
        newMarker.GetComponent<MinimapMarker>().locIndex = index;

        markers.Add(newMarker);
    }

    public void RemoveMarker()
    {

    }

    #endregion

    #region FS Methods

    void ReadFromFS()
    {
        FeatureService viewpointService = new FeatureService(_viewpointServiceURL);
        StartCoroutine(viewpointService.RequestFeatures("1=1", CreateViewpointFeatures, markerPrefab));

    }

    IEnumerator CreateViewpointFeatures(string data, GameObject prefab)
    {
        var results = JObject.Parse(data);
        var features = results["features"].Children();

        foreach (var feature in features)
        {
            //var attributes = feature.SelectToken("attributes");
            var geometry = feature.SelectToken("geometry");

            var lon = (double)geometry.SelectToken("x");
            var lat = (double)geometry.SelectToken("y");
            var alt = (double)geometry.SelectToken("z");

            int newIndex = locations.Count;
            locations.Add(new double3(lon, lat, alt));
            Debug.Log(locations[newIndex]);
            //AddMarker(newIndex, false);
            yield return null;
        }
    }

    void WriteToFS(int index)
    {
        //todo
    }


    #endregion
    //called from markers
    public void OnSelectMarker(int index)
    {
        StateManager.Instance.SetPlayerLocation(Convert.ToSingle(locations[index].x), Convert.ToSingle(locations[index].y), Convert.ToSingle(locations[index].z));
    }

    //called from button
    public void OnSaveLocation()
    {
        //get camera location
        ArcGISPoint cameraLoc = Camera.main.GetComponent<ArcGISLocationComponent>().Position;

        //add to Locations
        int newIndex = locations.Count;
        locations.Add(new double3(cameraLoc.X, cameraLoc.Y, cameraLoc.Z));

        AddMarker(newIndex, true);
        WriteToFS(newIndex);
    }
}
