using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Track
{
    // Task T8: "how far along the lap" for positions, the minimap and the bots (contract ITrackProgress,
    // docs/interfaces.md "Round 2"). Sits on the same object as TrackInfo; RaceFlow finds it with
    // GetComponent<ITrackProgress>(). The track builder hands over the road's centre points (SetRoadPoints);
    // RacingLineBuilder puts them in driving order from checkpoint 0, and TrackPolyline does the maths.
    // No points (or fewer than 3) = LapLength 0, and RaceFlow falls back to its line through the checkpoints.
    [RequireComponent(typeof(TrackInfo))]
    public class TrackProgress : MonoBehaviour, ITrackProgress
    {
        [SerializeField, Tooltip("Points along the middle of the road, one loop, any start and direction. Filled by the track builder (SetRoadPoints)")]
        private Vector3[] roadPoints = new Vector3[0];

        [SerializeField, Tooltip("Metres between racing line points on straights (bends keep every point)")]
        private float spacing = 6f;

        [SerializeField, Tooltip("A road point where the road turns more than this (degrees) always stays in the line")]
        private float keepTurnDegrees = 4f;

        private TrackPolyline line; // built on first use, so it does not matter whose Awake runs first

        public float LapLength { get { return Line.LapLength; } }

        public IReadOnlyList<Vector3> RacingLine { get { return Line.RacingLine; } }

        public float DistanceAlongLap(Vector3 worldPosition)
        {
            return Line.DistanceAlongLap(worldPosition);
        }

        public Vector3 PointAt(float distance)
        {
            return Line.PointAt(distance);
        }

        public Vector3 DirectionAt(float distance)
        {
            return Line.DirectionAt(distance);
        }

        // For track builders (editor scripts): store the road's centre points. The scene save keeps them.
        public void SetRoadPoints(IList<Vector3> points)
        {
            int count = points != null ? points.Count : 0;
            roadPoints = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                roadPoints[i] = points[i];
            }
            line = null; // rebuild with the new points
        }

        private TrackPolyline Line
        {
            get
            {
                if (line == null)
                {
                    line = BuildLine();
                }
                return line;
            }
        }

        // The racing line starts at checkpoint 0 (start/finish) and runs the way that checkpoint faces.
        private TrackPolyline BuildLine()
        {
            Vector3 startPosition = transform.position;
            Vector3 startForward = Vector3.zero;
            TrackInfo info = GetComponent<TrackInfo>();
            if (info != null && info.Checkpoints != null && info.Checkpoints.Length > 0 && info.Checkpoints[0] != null)
            {
                startPosition = info.Checkpoints[0].transform.position;
                startForward = info.Checkpoints[0].transform.forward;
            }
            List<Vector3> points = RacingLineBuilder.Build(roadPoints, startPosition, startForward, spacing, keepTurnDegrees);
            return new TrackPolyline(points);
        }

        // Inspector change: build the line again next time it is used.
        private void OnValidate()
        {
            line = null;
        }

        // Yellow racing line in the Scene view, green ball at the start line, so you can check it by eye.
        private void OnDrawGizmos()
        {
            IReadOnlyList<Vector3> points = RacingLine;
            if (points.Count < 3)
            {
                return;
            }
            Gizmos.color = Color.yellow;
            for (int i = 0; i < points.Count; i++)
            {
                Gizmos.DrawLine(points[i] + Vector3.up, points[(i + 1) % points.Count] + Vector3.up);
            }
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(points[0] + Vector3.up, 1f);
        }
    }
}
