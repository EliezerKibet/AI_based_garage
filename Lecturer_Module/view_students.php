<?php
session_start();
include_once 'database.php';

if (!isset($_SESSION['lecturer_id'])) {
    header("Location: login.php");
    exit;
}

$lecturer_name = $_SESSION['fullname'];

$database = new Database();
$db = $database->getConnection();

$query = "SELECT * FROM courses WHERE lecturer_id = :lecturer_id";
$stmt = $db->prepare($query);
$stmt->bindParam(':lecturer_id', $_SESSION['lecturer_id']);
$stmt->execute();
$courses = $stmt->fetchAll(PDO::FETCH_ASSOC);

$students = []; // Initialize an empty array to store student data

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    if (isset($_POST['course_id']) && !empty($_POST['course_id'])) {
        $course_id = $_POST['course_id'];

        // Using mysqli procedural
        $conn = mysqli_connect("localhost", "root", "", "eduauthorities");
        if (!$conn) {
            die("Connection failed: " . mysqli_connect_error());
        }

        $sql = "SELECT students.* FROM students 
                JOIN student_courses ON students.id = student_courses.student_id
                WHERE student_courses.course_id = ?";
        $stmt = mysqli_prepare($conn, $sql);
        mysqli_stmt_bind_param($stmt, "i", $course_id);
        mysqli_stmt_execute($stmt);
        $result = mysqli_stmt_get_result($stmt);
        $students = mysqli_fetch_all($result, MYSQLI_ASSOC);

        mysqli_stmt_close($stmt);
        mysqli_close($conn);
    } else {
        echo '<p>Please select a course.</p>';
    }
}
?>

<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>View Data</title>
    <link rel="stylesheet" href="CSS/main.css">
</head>
<body>
    <div class="sidebar">
        <h2>EduBridge</h2>
        <ul>                
            <li><a href="homepage.php"><i class="fas fa-home"></i> Dashboard</a></li>
            <li><a href="student_update.php"><i class="fas fa-edit"></i> Update Student Data</a></li>
            <li><a href="rand_student.php"><i class="fas fa-random"></i> Choose Random Student</a></li>
            <li><a href="phoneNum_search.php"><i class="fas fa-search"></i> Search by Phone Number</a></li>
            <li><a href="create_courses.php"><i class="fas fa-plus"></i> Manage Courses</a></li>
            <li><a href="login.php"><i class="fas fa-sign-out-alt"></i> Logout</a></li>
        </ul>
    </div>
    <div class="main-content">
        <div class="header">
            <div class="logo">
                <img src="../images/logo.png" alt="Logo" style="width: 100px; height: auto;">
            </div>
            <div class="user-info">
                <img src="../images/user-icon.png" alt="User Icon">
                <span class="name">Welcome, <?php echo htmlspecialchars($lecturer_name); ?></span>
                <a href="login.php">Logout</a>
            </div>
        </div>
        <div class="content">
            <div class="container">
                <form method="POST">
                    <h3>VIEW STUDENTS BY COURSE</h3>
                    <?php if (empty($courses)): ?>
                        <p>No courses available. Please add courses to view students.</p>
                    <?php else: ?>
                        <select name="course_id">
                            <option value="">Select a course</option>
                            <?php foreach ($courses as $course): ?>
                                <option value="<?= htmlspecialchars($course['id']) ?>"><?= htmlspecialchars($course['course_name']) ?></option>
                            <?php endforeach; ?>
                        </select>
                        <input type="submit" value="View Students">
                    <?php endif; ?>
                </form>

                <?php if (!empty($students)): ?>
                    <table>
                        <tr>
                            <th>Matric No</th>
                            <th>Full Name</th>
                            <th>Phone</th>
                            <th>Program Code</th>
                        </tr>
                        <?php foreach ($students as $student): ?>
                            <tr>
                                <td><?= htmlspecialchars($student['matric_no']) ?></td>
                                <td><?= htmlspecialchars($student['fullname']) ?></td>
                                <td><?= htmlspecialchars($student['phone']) ?></td>
                                <td><?= htmlspecialchars($student['program_code']) ?></td>
                            </tr>
                        <?php endforeach; ?>
                    </table>
                <?php elseif ($_SERVER['REQUEST_METHOD'] === 'POST'): ?>
                    <p>No students found for the selected course.</p>
                <?php endif; ?>
            </div>
        </div>
    </div>
</body>
</html>
