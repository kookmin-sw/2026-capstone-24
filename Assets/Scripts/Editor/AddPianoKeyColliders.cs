using UnityEngine;
using UnityEditor;

public class AddPianoKeyColliders : EditorWindow
{
    [MenuItem("Tools/Add Piano Key Colliders")]
    static void AddColliders()
    {
        if (!PianoEditorUtility.TryGetPianoRoot(out GameObject pianoRoot))
        {
            Debug.LogError("No piano root found. Select a piano root or place one in the scene.");
            return;
        }

        if (!PianoEditorUtility.TryGetKeyRigRoot(pianoRoot, out Transform root))
        {
            Debug.LogError("Selected object does not contain Piano_Rig/Root with 88 keys.");
            return;
        }

        // Standard piano: 88 keys (key_1 to key_88)
        // key_1 = A0 (MIDI 21), key_88 = C8 (MIDI 108)
        // White keys (in one octave starting from C): C, D, E, F, G, A, B
        // Black keys: C#, D#, F#, G#, A#
        // 
        // Pattern per octave (starting from C): W B W B W W B W B W B W
        // key_1 = A0, key_2 = A#0/Bb0, key_3 = B0
        // key_4 = C1, key_5 = C#1, key_6 = D1, key_7 = D#1, key_8 = E1
        // key_9 = F1, key_10 = F#1, key_11 = G1, key_12 = G#1, key_13 = A1, key_14 = A#1, key_15 = B1
        // Semitone offsets from C: 0=C(W), 1=C#(B), 2=D(W), 3=D#(B), 4=E(W), 5=F(W), 6=F#(B), 7=G(W), 8=G#(B), 9=A(W), 10=A#(B), 11=B(W)

        int addedCount = 0;
        int skippedCount = 0;

        Undo.SetCurrentGroupName("Add Piano Key Colliders");
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = 1; i <= 88; i++)
        {
            string keyName = "key_" + i;
            Transform keyTransform = root.Find(keyName);

            if (keyTransform == null)
            {
                Debug.LogWarning($"Key not found: {keyName}");
                continue;
            }

            // Skip if already has BoxCollider
            BoxCollider existingCollider = keyTransform.GetComponent<BoxCollider>();
            if (existingCollider != null)
            {
                skippedCount++;
                Debug.Log($"Skipping {keyName} - already has BoxCollider");
                continue;
            }

            // Find the _end child to determine bone length
            Transform endBone = keyTransform.Find(keyName + "_end");
            float boneLength = 0.00147f; // default fallback

            if (endBone != null)
            {
                boneLength = endBone.localPosition.y;
                if (boneLength <= 0)
                    boneLength = endBone.localPosition.magnitude;
            }

            // Determine if this key is black or white
            bool isBlack = IsBlackKey(i);

            // Calculate BoxCollider size based on bone length and key type
            float width, height;
            if (isBlack)
            {
                // Black keys are narrower and shorter
                width = boneLength * 0.10f;   // narrower than white
                height = boneLength * 0.15f;  // taller (they stick up more)
            }
            else
            {
                // White keys
                width = boneLength * 0.156f;  // ~0.00023 / 0.00147 ≈ 0.156 ratio from key_40
                height = boneLength * 0.136f;  // ~0.0002 / 0.00147 ≈ 0.136 ratio from key_40
            }

            // Add BoxCollider
            BoxCollider collider = Undo.AddComponent<BoxCollider>(keyTransform.gameObject);
            collider.center = new Vector3(0, boneLength / 2f, 0);
            collider.size = new Vector3(width, boneLength, height);

            addedCount++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"Piano Key Colliders on '{pianoRoot.name}': Added {addedCount}, Skipped {skippedCount} (already had collider)");
    }

    static bool IsBlackKey(int keyNumber)
    {
        // key_1 = A0 (MIDI note 21)
        // Convert key number to MIDI note: midi = keyNumber + 20
        // Then check semitone: (midi) % 12
        // Black keys have semitone offsets: 1(C#), 3(D#), 6(F#), 8(G#), 10(A#)
        int midi = keyNumber + 20;
        int semitone = midi % 12;
        // C=0, C#=1, D=2, D#=3, E=4, F=5, F#=6, G=7, G#=8, A=9, A#=10, B=11
        return semitone == 1 || semitone == 3 || semitone == 6 || semitone == 8 || semitone == 10;
    }
}
