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

$students = [];
$selectedStudent = null;
$message = '';

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    if (isset($_POST['course_id'])) {
        $course_id = $_POST['course_id'];

        $query = "SELECT students.* FROM students 
                  JOIN student_courses ON students.id = student_courses.student_id
                  WHERE student_courses.course_id = :course_id";
        $stmt = $db->prepare($query);
        $stmt->bindParam(':course_id', $course_id);
        $stmt->execute();
        $students = $stmt->fetchAll(PDO::FETCH_ASSOC);

        if (empty($students)) {
            $message = "No students enrolled in this course.";
        }
    }

    if (isset($_POST['student_id']) && !isset($_POST['course_id'])) {
        $student_id = $_POST['student_id'];
        $course_id = $_POST['course_id_hidden'];

        $stmt = $db->prepare("SELECT * FROM students WHERE id = :student_id");
        $stmt->bindParam(':student_id', $student_id);
        $stmt->execute();
        $selectedStudent = $stmt->fetch(PDO::FETCH_ASSOC);
    }

    if (isset($_POST['update_student'])) {
        $student_id = $_POST['student_id'];
        $fullname = $_POST['fullname'];
        $phone = $_POST['phone'];
        $program_code = $_POST['program_code'];

        $stmt = $db->prepare("SELECT * FROM students WHERE id = :student_id");
        $stmt->bindParam(':student_id', $student_id);
        $stmt->execute();
        $currentStudent = $stmt->fetch(PDO::FETCH_ASSOC);

        // Validate phone number
        if (!preg_match('/^0\d{9,10}$/', $phone)) {
            $message = "Please enter valid phone number.";
        } else {
            // Check if data has changed
            if ($fullname === $currentStudent['fullname'] && $phone === $currentStudent['phone']) {
                $message = "Nothing was updated.";
            } else {
                // Update the student
                $stmt = $db->prepare("UPDATE students SET fullname = :fullname, phone = :phone WHERE id = :student_id");
                $stmt->bindParam(':fullname', $fullname);
                $stmt->bindParam(':phone', $phone);
                $stmt->bindParam(':student_id', $student_id);

                if ($stmt->execute()) {
                    $message = "Student data updated successfully.";
                } else {
                    $message = "Failed to update student data.";
                }
                // Clear selected student and students list after update
                $selectedStudent = null;
                $students = [];
            }
        }
    }
}
?>

<!DOCTYPE html> 
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Update Data</title>
    <link rel="stylesheet" href="CSS/main.css">
</head>
<body>
    <div class="sidebar">
        <h2>EduBridge</h2>
        <ul>
            <li><a href="homepage.php"><i class="fas fa-home"></i> Dashboard</a></li>
            <li><a href="View_Students.php"><i class="fas fa-view"></i> View Students Data</a></li>
            <li><a href="rand_student.php"><i class="fas fa-random"></i> Choose Random Student</a></li>
            <li><a href="phoneNum_search.php"><i class="fas fa-search"></i> Search by Phone Number</a></li>
            <li><a href="Create_Courses.php"><i class="fas fa-plus"></i> Manage Courses</a></li>
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
                <h3>UPDATE STUDENT DATA</h3>
                <?php if ($message): ?>
                    <p><?= $message ?></p>
                <?php endif; ?>
                <form method="POST">
                    <label for="course_id">Select Course:</label>
                    <select name="course_id" id="course_id" required>
                        <option value="">Select a course</option>
                        <?php foreach ($courses as $course): ?>
                            <option value="<?= $course['id'] ?>" <?= (isset($course_id) && $course_id == $course['id']) ? 'selected' : '' ?>><?= $course['course_name'] ?></option>
                        <?php endforeach; ?>
                    </select>
                    <input type="submit" value="Select Course">
                </form>

                <?php if (!empty($students)): ?>
                    <form method="POST">
                        <input type="hidden" name="course_id_hidden" value="<?= htmlspecialchars($course_id) ?>">
                        <label for="student_id">Select Student:</label>
                        <select name="student_id" id="student_id" required>
                            <option value="">Select a student</option>
                            <?php foreach ($students as $student): ?>
                                <option value="<?= $student['id'] ?>" <?= (isset($student_id) && $student_id == $student['id']) ? 'selected' : '' ?>><?= htmlspecialchars($student['fullname']) ?></option>
                            <?php endforeach; ?>
                        </select>
                        <input type="submit" value="Select Student">
                    </form>
                <?php endif; ?>

                <?php if ($selectedStudent): ?>
                    <form method="POST">
                        <input type="hidden" name="update_student" value="1">
                        <input type="hidden" name="student_id" value="<?= htmlspecialchars($selectedStudent['id']) ?>">
                        <input type="hidden" name="course_id_hidden" value="<?= htmlspecialchars($course_id) ?>">
                        <label for="fullname">Full Name:</label>
                        <input type="text" name="fullname" id="fullname" value="<?= htmlspecialchars($selectedStudent['fullname']) ?>" required>
                        <label for="phone">Phone:</label>
                        <input type="text" name="phone" id="phone" value="<?= htmlspecialchars($selectedStudent['phone']) ?>" required>
                        <label for="program_code">Program Code:</label>
                        <input type="text" name="program_code" id="program_code" value="<?= htmlspecialchars($selectedStudent['program_code']) ?>" readonly>
                        <input type="submit" value="Update Student Data">
                    </form>
                <?php endif; ?>
            </div>
        </div>
    </div>
</body>
</html>
