<?php
include_once '../templates/header.php';
include_once 'db.php'; // Corrected the include path

// Fetch the number of courses and students dynamically
$lecturer_id = $_SESSION['lecturer_id'];

// Count total courses taught by the lecturer
$sql_courses = "SELECT COUNT(*) AS total_courses FROM courses WHERE lecturer_id = ?";
$stmt_courses = $conn->prepare($sql_courses);
$stmt_courses->bind_param("i", $lecturer_id);
$stmt_courses->execute();
$result_courses = $stmt_courses->get_result();
$row_courses = $result_courses->fetch_assoc();
$total_courses = $row_courses['total_courses'];
$stmt_courses->close();

// Count unique students enrolled in the courses taught by the lecturer
$sql_students = "SELECT COUNT(DISTINCT student_id) AS total_students FROM student_courses WHERE course_id IN (SELECT id FROM courses WHERE lecturer_id = ?)";
$stmt_students = $conn->prepare($sql_students);
$stmt_students->bind_param("i", $lecturer_id);
$stmt_students->execute();
$result_students = $stmt_students->get_result();
$row_students = $result_students->fetch_assoc();
$total_students = $row_students['total_students'];
$stmt_students->close();

// For simplicity, assume these values are fetched or calculated elsewhere in your system
$total_assignments_due = 7; // Example value
$total_upcoming_events = 4; // Example value

?>

<link rel="stylesheet" href="CSS/main.css">

<div class="dashboard">
    <div class="header">
        <div class="logo">
            <img src="../images/logo.png" alt="Logo" style="width: 100px; height: auto;">
        </div>
        <div class="user-info">
            <img src="../images/user-icon.png" alt="User Icon" >
            <div class="name">
                Welcome, <?= $_SESSION['fullname'] ?>
            </div>
            <a href="login.php">Logout</a>
        </div>
    </div>

    <div class="card-container">
        <div class="card blue">
            <h3>Total Courses</h3>
            <div class="stats"><?= $total_courses ?></div>
            <a href="Create_courses.php">Manage Courses</a>
        </div>

        <div class="card red">
            <h3>Total Students</h3>
            <div class="stats"><?= $total_students ?></div>
            <a href="view_students.php">View Students</a>
        </div>

        <div class="card purple">
            <h3>Assignments Due</h3>
            <div class="stats"><?= $total_assignments_due ?></div>
            <a href="#">View Assignments</a>
        </div>

        <div class="card yellow">
            <h3>Upcoming Events</h3>
            <div class="stats"><?= $total_upcoming_events ?></div>
            <a href="#">View Events</a>
        </div>
    </div>
</div>

<?php include_once '../templates/footer.php'; ?>
