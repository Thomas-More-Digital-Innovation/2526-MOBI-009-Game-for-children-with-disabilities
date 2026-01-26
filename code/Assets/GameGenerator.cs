using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;
using UnityEngine.InputSystem;

// The whole point of this script is to basically take the height and width of the table it found,
// split it into 6 columns and 3 rows and create points in the middle of each cell on the grid created
// we will use this for spawning objects and car respawning :)
public class GameGenerator : MonoBehaviour
{
    [SerializeField] private MRUKAnchor.SceneLabels tableLabel = MRUKAnchor.SceneLabels.TABLE;
    [SerializeField] private int cellColumns = 6;
    [SerializeField] private int cellRows = 3;
    [SerializeField, Range(0.5f, 1f)] private float surfaceMargin = 0.85f;

    private MRUKAnchor Table;
    public Vector3[] cellCenters;
    private float cellWidth;
    private float cellHeight;
    private float planeWidth;
    private float planeHeight;
    private int[] carCells;
    private int[] ObjectCells;

    [Header("Prefabs")]
    [SerializeField] private GameObject carObject;
    [SerializeField] private GameObject[] gameObjects;
    [SerializeField, Range(1, 16)] private int numberGameObjects = 2;

    [Header("Respawn Objects Input")]
    [Tooltip("Bind this to a button in your Input Actions, then drag it here.")]
    [SerializeField] private InputActionReference respawnObjectsAction;

    private HashSet<int> occupiedCells = new HashSet<int>();
    private readonly List<GameObject> spawnedObjects = new List<GameObject>();

    public enum CellSpawnType
    {
        Car,
        Object
    }

    public CellSpawnType[] spawnType;

    private void Start()
    {
        Invoke(nameof(TryFindTable), 0.5f);
    }

    private void OnEnable()
    {
        if (respawnObjectsAction != null && respawnObjectsAction.action != null)
        {
            respawnObjectsAction.action.performed += OnRespawnObjects;
            respawnObjectsAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (respawnObjectsAction != null && respawnObjectsAction.action != null)
        {
            respawnObjectsAction.action.performed -= OnRespawnObjects;
            respawnObjectsAction.action.Disable();
        }
    }

    private void OnRespawnObjects(InputAction.CallbackContext ctx)
    {
        if (Table == null) return; // table not found yet, chill

        ClearSpawnedObjects();
        occupiedCells.Clear();
        spawnObjectsOnce();
    }

    private void ClearSpawnedObjects()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] != null)
                Destroy(spawnedObjects[i]);
        }
        spawnedObjects.Clear();
    }

    private void TryFindTable()
    {
        var room = MRUK.Instance?.GetCurrentRoom();
        if (room == null) return;

        var anchorObjects = room.Anchors;
        if (anchorObjects == null) return;

        foreach (var anchorObject in anchorObjects)
        {
            if (anchorObject.Label.Equals(tableLabel))
            {
                Table = anchorObject;

                planeHeight = Table.PlaneRect.Value.size.y;
                planeWidth = Table.PlaneRect.Value.size.x;

                cellWidth = planeWidth / cellColumns;
                cellHeight = planeHeight / cellRows;

                if (transform.parent != null)
                {
                    transform.parent.position = Table.transform.position;
                    transform.parent.rotation = Table.transform.rotation;
                }

                spawnGrid();
                break;
            }
        }
    }

    // We will imagine that we split the generate grid into two parts, for each half we want to take the middle
    // and appoint it as our "object" generation spots :)
    private void spawnGrid()
    {
        cellCenters = new Vector3[cellColumns * cellRows];
        spawnType = new CellSpawnType[cellColumns * cellRows];

        int leftMidCol = (cellColumns / 2) / 2;
        int rightMidCol = (cellColumns / 2) + leftMidCol;
        int midRow = cellRows / 2;

        float usedPlaneWidth = planeWidth * surfaceMargin;
        float usedPlaneHeight = planeHeight * surfaceMargin;

        float usedCellWidth = usedPlaneWidth / cellColumns;
        float usedCellHeight = usedPlaneHeight / cellRows;

        int index = 0;

        for (int row = 0; row < cellRows; row++)
        {
            for (int col = 0; col < cellColumns; col++)
            {
                float x = (-usedPlaneWidth / 2f) + (usedCellWidth * col) + (usedCellWidth / 2f);
                float z = (-usedPlaneHeight / 2f) + (usedCellHeight * row) + (usedCellHeight / 2f);

                bool isLeftMiddle = (col == leftMidCol && row == midRow);
                bool isRightMiddle = (col == rightMidCol && row == midRow);

                cellCenters[index] = Table.transform.TransformPoint(new Vector3(x, z, 0f));
                spawnType[index] = (isLeftMiddle || isRightMiddle) ? CellSpawnType.Car : CellSpawnType.Object;

                index++;
            }
        }

        List<int> cars = new List<int>();
        List<int> objs = new List<int>();

        for (int i = 0; i < spawnType.Length; i++)
        {
            if (spawnType[i] == CellSpawnType.Car) cars.Add(i);
            else objs.Add(i);
        }

        carCells = cars.ToArray();
        ObjectCells = objs.ToArray();

        spawnCar();
        spawnObjectsOnce();
    }

    private void spawnObjectsOnce()
    {
        if (gameObjects == null || gameObjects.Length == 0) return;

        List<int> free = new List<int>(ObjectCells.Length);
        for (int i = 0; i < ObjectCells.Length; i++)
        {
            int idx = ObjectCells[i];
            if (!occupiedCells.Contains(idx))
                free.Add(idx);
        }

        for (int i = free.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (free[i], free[j]) = (free[j], free[i]);
        }

        int spawnCount = Mathf.Min(numberGameObjects, free.Count);

        for (int i = 0; i < spawnCount; i++)
        {
            int cellIndex = free[i];
            occupiedCells.Add(cellIndex);

            GameObject prefab = gameObjects[Random.Range(0, gameObjects.Length)];
            if (prefab == null) continue;

            var go = Instantiate(
                prefab,
                cellCenters[cellIndex],
                Quaternion.Euler(0f, Table.transform.eulerAngles.y, 0f)
            );

            spawnedObjects.Add(go);
        }
    }

    private void spawnCar()
    {
        if (carObject == null) return;

        int cellIndex = SelectRandomSpawnSpot(true);
        if (cellIndex < 0) return;

        Instantiate(carObject, cellCenters[cellIndex], Quaternion.identity);
    }

    public int SelectRandomSpawnSpot(bool Car)
    {
        var pool = Car ? carCells : ObjectCells;
        if (pool == null || pool.Length == 0) return -1;
        return pool[Random.Range(0, pool.Length)];
    }
}
