using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;



public class PackageDownloader: EditorWindow
{


    [MenuItem("Window/PackageDownloader")]
    public static void ShowExample()
    {
        PackageDownloader wnd = GetWindow<PackageDownloader>();
        wnd.titleContent = new GUIContent("Downloader Window");
    }


    public GoogleDriveManager manager;
    public GoogleSheetsJSONWrapper sheetsData;
    public DropdownField headerSelectDropdown;
    public TextField sheetInformation, pageInformation;
    public List<string> googleDocHeaders;

    public void Awake()
    {
        filters = new List<(DropdownField, TextField)>();
    }


    public List<(DropdownField, TextField)> filters;
    public Foldout group;

    private Button getDataButton, downloadButton;

    //A small bold, underlined heading used to break the window into clear steps.
    private Label MakeSectionHeader(string text)
    {
        Label header = new Label(text);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.fontSize = 13;
        header.style.marginTop = 10;
        header.style.marginBottom = 2;
        header.style.borderBottomWidth = 1;
        header.style.borderBottomColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        return header;
    }

    public void RemoveFilter()
    {
        if (filters.Count > 0)
        {
            (DropdownField, TextField) oldest = filters[filters.Count - 1];
            group.Remove(oldest.Item1);
            group.Remove(oldest.Item2);
            filters.Remove(oldest);
        }
    }
    public void AddNewFilter()
    {
        DropdownField dropdown = new DropdownField();
        dropdown.label = "Row Name:";
        group.Add(dropdown);
        if (googleDocHeaders != null)
        {
            dropdown.choices = googleDocHeaders;
        }
        TextField value = new TextField();
        value.label = "Row Value:";
        group.Add(value);

        if (filters == null)
            filters = new List<(DropdownField, TextField)>();

        filters.Add(new(dropdown, value));
    }
    public void CreateGUI()
    {
        rootVisualElement.style.paddingLeft = 8;
        rootVisualElement.style.paddingRight = 8;
        rootVisualElement.style.paddingBottom = 8;

        rootVisualElement.Add(MakeSectionHeader("1 · Load Sheet"));

        sheetInformation = new TextField();
        sheetInformation.label = "Document Id:";
        sheetInformation.value = EditorPrefs.GetString("DocumentID");
        //Persist the value as it changes instead of writing every frame in OnGUI
        sheetInformation.RegisterValueChangedCallback(evt => EditorPrefs.SetString("DocumentID", evt.newValue));
        rootVisualElement.Add(sheetInformation);

        pageInformation = new TextField();
        pageInformation.label = "Page Name:";
        pageInformation.value = EditorPrefs.GetString("PageName");
        pageInformation.RegisterValueChangedCallback(evt => EditorPrefs.SetString("PageName", evt.newValue));

        rootVisualElement.Add(pageInformation);

        getDataButton = new Button();
        getDataButton.text = "Get Sheet Data";
        getDataButton.clicked += DownloadJSON;
        getDataButton.style.height = 26;
        getDataButton.style.marginTop = 4;
        getDataButton.style.marginBottom = 6;
        rootVisualElement.Add(getDataButton);




        //Once we have the json, apply filters
        rootVisualElement.Add(MakeSectionHeader("2 · Filter Submissions"));

        group = new Foldout();
        group.text = "Filters";


        Button newFilter = new Button();
        newFilter.text = "New Filter";
        newFilter.clicked += AddNewFilter;
        group.Add(newFilter);

        Button removeFilter = new Button();
        removeFilter.text = "Remove Filter";
        removeFilter.clicked += RemoveFilter;
        group.Add(removeFilter);

        rootVisualElement.Add(group);




        Button filterButton = new Button();
        filterButton.text = "Apply Filter";
        filterButton.clicked += FilterJSONEntries;
        filterButton.style.height = 26;
        filterButton.style.marginTop = 6;
        rootVisualElement.Add(filterButton);

        //Stable home for the match count so it doesn't jump to the bottom of the window
        matchLabel = new Label("No filter applied yet");
        matchLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        matchLabel.style.marginTop = 4;
        matchLabel.style.marginBottom = 4;
        rootVisualElement.Add(matchLabel);



        //What should we call the file
        rootVisualElement.Add(MakeSectionHeader("3 · Name & Download"));

        nameingDropwdown = new DropdownField();
        nameingDropwdown.label = "Name packages after:";
        rootVisualElement.Add(nameingDropwdown);
        if (googleDocHeaders != null)
        {
            nameingDropwdown.choices = googleDocHeaders;
        }
        //Now finally, download
        downloadButton = new Button();
        downloadButton.text = "Download Matches";
        downloadButton.clicked += DownloadFromMatches;
        downloadButton.style.height = 30;
        downloadButton.style.marginTop = 8;
        rootVisualElement.Add(downloadButton);


    }
    DropdownField nameingDropwdown;
    public async void DownloadJSON()
    {

        if (manager == null)
        {
            manager = new GoogleDriveManager();
        }

        //Give feedback while the request is in flight so the window doesn't look frozen
        getDataButton.SetEnabled(false);
        getDataButton.text = "Loading…";
        try
        {
            sheetsData = await manager.GetDocumentJSON(sheetInformation.value,pageInformation.value);
        }
        finally
        {
            getDataButton.text = "Get Sheet Data";
            getDataButton.SetEnabled(true);
        }

        if (sheetsData == null)
        {
            Debug.LogError("Could not load sheet data. Double-check the Document Id and Page Name.");
            return;
        }
        googleDocHeaders = sheetsData.headers.ToList();

        if (filters == null)
            filters = new List<(DropdownField, TextField)>();


        foreach ((DropdownField,TextField) filter in filters)
        {
            filter.Item1.choices = sheetsData.headers.ToList();
        }
        nameingDropwdown.choices = sheetsData.headers.ToList();
    }


    public List<string[]> matches;
    public Label matchLabel;
    public void FilterJSONEntries()
    {
        if (sheetsData == null)
        {
            Debug.LogError("No sheet data loaded yet. Click \"Get Sheet Data\" first.");
            return;
        }

        List<string> headers = new List<string>();
        List<string> values = new List<string>();

        foreach ((DropdownField,TextField) filter in filters)
        {
            headers.Add(filter.Item1.value);
            values.Add(filter.Item2.value);
        }
        matches = sheetsData.CompoundFilter(headers.ToArray(), values.ToArray());

        //Double check / purge any blank entries
        for (int x = 0; x < matches.Count; x++)
        {
            matches[x] = matches[x].Where(s => !string.IsNullOrEmpty(s)).ToArray();
        }


        //now lets pring them for my own sake
        for (int x = 0; x < matches.Count; x++)
        {
            string info = "";
            for(int y=0;y< matches[x].Length; y++)
            {
                info += "\n[" + y + "]\"" + matches[x][y] + "\"";
            }
            Debug.Log(info);
        }

        matchLabel.text = matches.Count + (matches.Count == 1 ? " match found" : " matches found");
    }

    public async void DownloadFromMatches()
    {
        if (sheetsData == null || matches == null)
        {
            Debug.LogError("Nothing to download yet. Load the sheet and apply a filter first.");
            return;
        }

        List<(string,string)> ids = sheetsData.GetDownloadIDs("Unitypackage of your entire project including everything listed above", matches,nameingDropwdown.value);

        //Batch together our asset editing so unity doesn't import after each one
        AssetDatabase.StartAssetEditing();

        if (manager == null)
        {
            manager = new GoogleDriveManager();
        }

        downloadButton.SetEnabled(false);
        downloadButton.text = "Downloading…";
        try
        {
            for (int x = 0; x < ids.Count; x++)
            {
                string fileId = ids[x].Item1;
                Debug.Log("File Id:"+fileId);

                //The id column is expected to hold a "...=<driveId>" style value. Skip rows
                //that don't so one bad cell can't abort the whole batch.
                string[] fileIdParts = fileId.Split('=');
                if (fileIdParts.Length < 2)
                {
                    Debug.LogError("Skipping \"" + ids[x].Item2 + "\": could not find a download id in \"" + fileId + "\"");
                    continue;
                }
                fileId = fileIdParts[1];

                EditorUtility.DisplayProgressBar("Downloading Files", "Current Package:" + ids[x].Item2, (x * 1.0f) / (ids.Count * 1.0f));
                await manager.GetFile(fileId, ids[x].Item2, Application.dataPath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Download failed: " + e);
        }
        finally
        {
            //Always tear these down, even if a download threw, so the UI doesn't get stuck
            EditorUtility.ClearProgressBar();
            AssetDatabase.StopAssetEditing();
            downloadButton.text = "Download Matches";
            downloadButton.SetEnabled(true);
        }
    }
    

}
