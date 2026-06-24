using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
public class PackageGraderTool : EditorWindow
{


    string PACKAGE_PATH = "Assets/Packages";

    private string currentlySelectedPackage;
    private string packageFolderPath;
    string[] KNOWN_FOLDERS = { "Assets/Scenes", "Assets/Packages", "Assets/Editor" };

    private VisualElement m_RightPane;
    private ListView m_leftPane;
    private TextElement m_RightHeader, m_packagePathHeader;
    [MenuItem("Window/PackageGraderTool")]
    public static void ShowExample()
    {
        PackageGraderTool wnd = GetWindow<PackageGraderTool>();
        wnd.titleContent = new GUIContent("Grader Window");
    }


    private Button deleteButton, importButton, openSceneButton;
    public IntegerField sceneIndex;


    public GoogleDriveManager manager;
    public void Awake()
    {
        Debug.Log("Awake");

    }

    public void SelectFolder()
    {
        //Ask which folder to look at for the .unityPackage files
        PACKAGE_PATH = EditorUtility.OpenFolderPanel("Select the folder with the .unitypackages", "", "");


        Debug.Log("PACKAGE_PATH:" + PACKAGE_PATH);
        //Add the package path to the known folders
        KNOWN_FOLDERS[1] = PACKAGE_PATH;
        PopulateLeftPane();

    }
    public void CreateGUI()
    {

        m_packagePathHeader = new TextElement();
        m_packagePathHeader.style.unityTextAlign = TextAnchor.MiddleCenter;
        m_packagePathHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        m_packagePathHeader.style.whiteSpace = WhiteSpace.Normal;
        m_packagePathHeader.style.marginTop = 4;
        m_packagePathHeader.text = PACKAGE_PATH;


        Button selectFolderButton = new Button();
        selectFolderButton.text = "Select Folder";
        selectFolderButton.clicked += SelectFolder;
        selectFolderButton.style.height = 26;
        rootVisualElement.Add(m_packagePathHeader);
        rootVisualElement.Add(selectFolderButton);

        //Which scene should we load
        sceneIndex = new IntegerField();
        sceneIndex.label = "What scene # should we load:";
        sceneIndex.value = 0;
        rootVisualElement.Add(sceneIndex);

        // Create a two-pane view with the left pane being fixed with
        var splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);

        // Add the view to the visual tree by adding it as a child to the root element
        rootVisualElement.Add(splitView);

        // A TwoPaneSplitView always needs exactly two child elements
        m_leftPane = new ListView();
        splitView.Add(m_leftPane);
        m_leftPane.fixedItemHeight = 35;


        //The right panel is all the controls
        m_RightPane = new VisualElement();
        m_RightPane.style.paddingLeft = 8;
        m_RightPane.style.paddingRight = 8;
        m_RightPane.style.paddingTop = 8;
        splitView.Add(m_RightPane);

        importButton = new Button();
        importButton.clicked += ImportPackage;
        importButton.text = "Import Package";
        importButton.style.marginBottom = 4;
        importButton.style.height = 28;


        deleteButton = new Button();
        deleteButton.clicked += DeletePackage;
        deleteButton.text = "Clear Project";
        deleteButton.style.marginBottom = 4;
        deleteButton.style.height = 28;

        openSceneButton = new Button();
        openSceneButton.clicked += OpenSceneInPackage;
        openSceneButton.text = "Open nth Scene in Package";
        openSceneButton.style.marginBottom = 4;
        openSceneButton.style.height = 28;


        m_RightHeader = new TextElement();
        m_RightHeader.style.unityTextAlign = TextAnchor.MiddleCenter;
        m_RightHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        m_RightHeader.style.marginBottom = 8;
        m_RightHeader.style.whiteSpace = WhiteSpace.Normal;
        m_RightHeader.text = "Select a package on the left";


        m_RightPane.Add(m_RightHeader);


        //Add the three buttons
        m_RightPane.Add(importButton);
        m_RightPane.Add(openSceneButton);
        m_RightPane.Add(deleteButton);

        //Import / Open need a selected package; keep them disabled until one is picked.
        //"Clear Project" stays enabled since it doesn't depend on a selection.
        importButton.SetEnabled(false);
        openSceneButton.SetEnabled(false);

        PopulateLeftPane();



    }
    private void PopulateLeftPane()
    {
        //Update the path header
        m_packagePathHeader.style.unityTextAlign = TextAnchor.MiddleCenter;
        m_packagePathHeader.text = PACKAGE_PATH;

        string[] packages = GetPackages();

        m_leftPane.Clear();

        //Repopulating clears the selection, so reset anything that depended on it
        currentlySelectedPackage = null;
        packageFolderPath = null;
        if (importButton != null) importButton.SetEnabled(false);
        if (openSceneButton != null) openSceneButton.SetEnabled(false);
        if (m_RightHeader != null) m_RightHeader.text = "Select a package on the left";

        //Show how many packages are in the selected folder
        m_packagePathHeader.text = PACKAGE_PATH + "\n(" + packages.Length + " package" + (packages.Length == 1 ? "" : "s") + " found)";

        if (packages.Length == 0)
        {
            Repaint();
            return;
        }

        //The left plane will have all the packages
        m_leftPane.makeItem = () => new Label();
        m_leftPane.bindItem = (item, index) => {
            (item as Label).text = packages[index].Substring(PACKAGE_PATH.Length);
        };
        m_leftPane.itemsSource = packages;
        //Unsubscribe first so re-populating (e.g. picking a new folder) doesn't stack handlers
        m_leftPane.selectionChanged -= OnPackageSelection;
        m_leftPane.selectionChanged += OnPackageSelection;


        Repaint();
    }
    private void OnPackageSelection(IEnumerable<object> selectedItems)
    {
        // Get the selected folder
        currentlySelectedPackage = selectedItems.FirstOrDefault() as string;
        bool hasSelection = !string.IsNullOrEmpty(currentlySelectedPackage);

        m_RightHeader.text = hasSelection
            ? System.IO.Path.GetFileName(currentlySelectedPackage)
            : "Select a package on the left";

        //Buttons that act on the selection are only usable once we have one
        importButton.SetEnabled(hasSelection);
        openSceneButton.SetEnabled(hasSelection);

        packageFolderPath = null;
    }


    public string[] GetPackages()
    {
        if (PACKAGE_PATH == null || PACKAGE_PATH == string.Empty)
        {
            Debug.LogError("No path was specified.");
            return new string[0];
        }
        //Read from the UnimportedPackages folder
        string[] packages = Directory.GetFiles(PACKAGE_PATH, "*.unitypackage", SearchOption.TopDirectoryOnly);
        if (packages.Length == 0) {
            Debug.LogError("No Packages Found in Folder");
        }
        return packages;

    }


    private void ImportPackage()
    {
        if (string.IsNullOrEmpty(currentlySelectedPackage))
        {
            Debug.LogError("Select a package from the list first.");
            return;
        }
        AssetDatabase.ImportPackage(currentlySelectedPackage, false);
        // Refresh the AssetDatabase after all the changes
        AssetDatabase.Refresh();
    }

    private void DeletePackage()
    {
        //This wipes imported content out of Assets/ — guard it behind a confirmation
        //so it can't be triggered by a stray click mid-grading.
        bool confirmed = EditorUtility.DisplayDialog(
            "Clear Project?",
            "This will delete all imported content from the Assets folder (everything except the Editor and Packages folders).\n\nThis cannot be undone. Continue?",
            "Clear Project",
            "Cancel");

        if (!confirmed)
            return;

        DirectoryInfo d = new DirectoryInfo("Assets");

        foreach (var file in d.GetDirectories("*"))
        {
            string name = file.FullName;
            Debug.Log("Considering File:" + name);

            if (!name.Contains("Editor") && !name.Contains("Packages"))
            {
                Debug.Log("Deleting:" + name);
                FileUtil.DeleteFileOrDirectory(file.FullName);
                FileUtil.DeleteFileOrDirectory(file.FullName + ".meta");
            }
            AssetDatabase.Refresh();

        }

        foreach (var file in d.GetFiles("*"))
        {
            string name = file.FullName;
            Debug.Log("Considering File:" + name);

            if (!name.Contains("Editor") && !name.Contains("Packages")) {
                Debug.Log("Deleting:" + name);
                FileUtil.DeleteFileOrDirectory(file.FullName);
            }
            AssetDatabase.Refresh();

        }


    }

    private void FindPackageDirectory()
    {

        var folders = AssetDatabase.GetSubFolders("Assets");
        foreach (var folder in folders)
        {
            if (!KNOWN_FOLDERS.Contains(folder)) {
                packageFolderPath = folder;
                Debug.Log("Found the package folder path:" + packageFolderPath);
                return;
            }
        }

        Debug.LogError("Cannot get package path, are you sure the package has been imported?");
        packageFolderPath = null;

    }

    private void OpenSceneInPackage()
    {
        FindPackageDirectory();
        if (string.IsNullOrEmpty(packageFolderPath))
        {
            //FindPackageDirectory already logged why
            return;
        }

        var texturePackageNames = Directory.GetFiles(packageFolderPath, "*.unity", SearchOption.AllDirectories);

        if (texturePackageNames.Length == 0)
        {
            Debug.LogError("No scenes found in folder " + currentlySelectedPackage);
            return;
        }

        //Clamp the requested scene number into range so a bad value can't crash the tool
        int index = Mathf.Clamp(sceneIndex.value, 0, texturePackageNames.Length - 1);
        if (index != sceneIndex.value)
        {
            Debug.LogWarning("Scene #" + sceneIndex.value + " is out of range. Opening scene #" + index + " instead (" + texturePackageNames.Length + " scene(s) found).");
        }
        EditorSceneManager.OpenScene(texturePackageNames[index]);
    }
}
