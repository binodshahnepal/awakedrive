from dms_perception.metrics import PerclosTracker, eye_aspect_ratio, mouth_aspect_ratio


def test_eye_aspect_ratio_open_eye_greater_than_closed():
    open_eye = [(0, 0), (1, 2), (2, 2), (4, 0), (2, -2), (1, -2)]
    closed_eye = [(0, 0), (1, 0.2), (2, 0.2), (4, 0), (2, -0.2), (1, -0.2)]
    assert eye_aspect_ratio(open_eye) > eye_aspect_ratio(closed_eye)


def test_mouth_aspect_ratio_matches_eye_formula_shape():
    closed_mouth = [(0, 0), (1, 0.1), (2, 0.1), (4, 0), (2, -0.1), (1, -0.1)]
    open_mouth = [(0, 0), (1, 3), (2, 3), (4, 0), (2, -3), (1, -3)]
    assert mouth_aspect_ratio(open_mouth) > mouth_aspect_ratio(closed_mouth)


def test_perclos_tracker_all_closed_is_one():
    tracker = PerclosTracker(window_seconds=10.0, closed_eye_ear_threshold=0.20)
    value = 0.0
    for t in range(10):
        value = tracker.update(float(t), ear=0.10)  # always below threshold
    assert value == 1.0


def test_perclos_tracker_all_open_is_zero():
    tracker = PerclosTracker(window_seconds=10.0, closed_eye_ear_threshold=0.20)
    value = 1.0
    for t in range(10):
        value = tracker.update(float(t), ear=0.35)  # always above threshold
    assert value == 0.0
