-- phpMyAdmin SQL Dump
-- version 5.2.1
-- https://www.phpmyadmin.net/
--
-- Host: 127.0.0.1
-- Generation Time: Jul 22, 2024 at 09:08 PM
-- Server version: 10.4.32-MariaDB
-- PHP Version: 8.2.12

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Database: `eduauthorities`
--

-- --------------------------------------------------------

--
-- Table structure for table `courses`
--

CREATE TABLE `courses` (
  `id` int(11) NOT NULL,
  `course_code` varchar(10) NOT NULL,
  `course_name` varchar(100) NOT NULL,
  `group_number` int(11) NOT NULL,
  `lecturer_id` int(11) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

--
-- Dumping data for table `courses`
--

INSERT INTO `courses` (`id`, `course_code`, `course_name`, `group_number`, `lecturer_id`) VALUES
(4, 'IAS 2589', 'OPERATING SYSTEM', 0, 2),
(5, 'ICS 2133', 'MATHEMATICS', 0, 2),
(7, 'ICS 2132', 'PROJECT MANAGEMENT', 0, 1),
(11, 'ICS 2233', 'ACOUSTICS', 0, 1),
(16, 'ICS 2162', 'GREEN AWARENESS', 0, 2),
(39, 'IAS 2154', 'INTRO TO WEB DESIGN', 1, 10),
(40, 'IAS 2154', 'INTRO TO WEB DESIGN', 2, 2);

-- --------------------------------------------------------

--
-- Table structure for table `lecturers`
--

CREATE TABLE `lecturers` (
  `id` int(11) NOT NULL,
  `username` varchar(50) NOT NULL,
  `password` varchar(255) NOT NULL,
  `fullname` varchar(100) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

--
-- Dumping data for table `lecturers`
--

INSERT INTO `lecturers` (`id`, `username`, `password`, `fullname`) VALUES
(1, 'Shalu', '$2y$10$nUbEoFIRGZ19F5m3K/oykuBriAD.fYSFgG0NRAaYmw6.li2EBiiPO', 'YUVA SHALINI MATHIALAGAN'),
(2, 'Joshua Pandi', '$2y$10$qqN1Uo8/aMaulEwzD9TM/OoFBHCRoQ30xO6XcDx8ZQO36ap/1bUkK', 'Joshua Kumar '),
(3, 'Kamala', '$2y$10$scZaoej3UfNoLwroplAVB.SJJNug7KXxcYSCTr36yB3IDtze2cJYq', 'Kamala arumugam'),
(7, 'Yamuna', '$2y$10$POEfRQ/mtqDk9gWOh9SmF.YRuvwZ4YAsWPAswZuxEf.oa07qkWr/y', 'Yamuna Sivam'),
(8, 'FADHIL ', '$2y$10$nTCfIaeC9XhzmhaMQQVKo.8cLrLU0Hd2m/Y71HXjNXTZFX981OUzu', 'FADHIL MUHAMMAD'),
(9, 'gopal', '$2y$10$GwFO/rGkKcmBD4f2NEhqpevZPxbUHOGE18aEhSw4QH2mmdbJoiPhW', 'gopal kumar'),
(10, 'Karthik', '$2y$10$YA/4z9I5YLj.5HndB/it8ukNQBO7SZ6buL0HdZAg7xEVJDYnPAJJS', 'KARTHIK RAJ PARTHIBAN'),
(11, 'Pratab', '$2y$10$aLdkWGhOzDlODOPGlXIwPO/5RYj3oHVk1Te1m/lpNfat6d4P7Vz96', 'Pratab Raj'),
(13, 'Kathir', '$2y$10$7FHZl9l9bARl3Ml0f61LMuccF20aM8/mZavR4IM3Sr7SE3OHn1BDK', 'KATHIR RAJ');

-- --------------------------------------------------------

--
-- Table structure for table `students`
--

CREATE TABLE `students` (
  `id` int(11) NOT NULL,
  `matric_no` varchar(20) NOT NULL,
  `fullname` varchar(100) NOT NULL,
  `phone` varchar(15) NOT NULL,
  `program_code` varchar(10) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

--
-- Dumping data for table `students`
--

INSERT INTO `students` (`id`, `matric_no`, `fullname`, `phone`, `program_code`) VALUES
(1, '4223003681', 'Mageswary', '0123260814', 'IT302'),
(2, '4225658977', 'Anupriya', '0123501041', 'IT301'),
(3, '4223005481', 'SHAMALA ANNAVI', '0123501041', 'IT301'),
(7, '4225658978', 'yuvashahmila', '0123501041', 'IT401'),
(8, '4231001961', 'Joshua Kumar ', '0165898899', 'IT402'),
(11, '4223003682', 'SHAMELAN ', '0126857895', 'IT405'),
(13, '4223003687', 'MESHARANI', '0126857896', 'IT302'),
(15, '4231234569', 'ANUSHREE', '0123501042', 'IT402'),
(16, '4231001698', 'YoYo ', '0123260814', 'IT401'),
(17, '4223003685', 'Magesi', '0126857897', 'IT301'),
(18, '4225658979', 'YUVA SHALINI MATHIALAGAN', '0123501041', 'IT301'),
(21, '4231001487', 'Billa', '0123260814', 'IT301'),
(22, '4234789856', 'Meghala', '52533434', 'IT301'),
(23, '4223005488', 'Rajeswary ', '0162864447', 'IT401'),
(24, '4223569977', 'ANUPOMA', '0162864459', 'IT403'),
(25, '4225005681', 'POOMMALAR A/P MATHIVANAN', '0163154240', 'IT403'),
(28, '4226003681', 'Sakthiswary a/p parthiban', '0176468022', 'IT301'),
(33, '4226332158', 'Siti binti Abdul', '0120253369', 'IT301'),
(34, '422365182', 'Muthu Kumar', '0142536187', 'IT301');

-- --------------------------------------------------------

--
-- Table structure for table `student_courses`
--

CREATE TABLE `student_courses` (
  `id` int(11) NOT NULL,
  `student_id` int(11) NOT NULL,
  `course_id` int(11) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

--
-- Dumping data for table `student_courses`
--

INSERT INTO `student_courses` (`id`, `student_id`, `course_id`) VALUES
(1, 7, 2),
(2, 8, 3),
(3, 11, 3),
(4, 13, 4),
(5, 15, 5),
(6, 16, 2),
(7, 17, 3),
(8, 21, 5),
(9, 23, 5),
(10, 23, 7),
(11, 23, 11),
(12, 24, 2),
(13, 24, 3),
(14, 24, 4),
(15, 24, 11),
(16, 25, 39),
(17, 26, 39),
(18, 27, 39),
(19, 28, 4),
(20, 28, 5),
(21, 28, 7),
(22, 28, 39),
(23, 29, 39),
(24, 30, 7),
(25, 30, 16),
(26, 30, 39),
(27, 31, 39),
(28, 31, 4),
(29, 32, 39),
(30, 32, 16),
(31, 33, 39),
(32, 34, 40);

--
-- Indexes for dumped tables
--

--
-- Indexes for table `courses`
--
ALTER TABLE `courses`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `course_code` (`course_code`,`course_name`,`group_number`),
  ADD KEY `lecturer_id` (`lecturer_id`);

--
-- Indexes for table `lecturers`
--
ALTER TABLE `lecturers`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `username` (`username`);

--
-- Indexes for table `students`
--
ALTER TABLE `students`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `matric_no` (`matric_no`);

--
-- Indexes for table `student_courses`
--
ALTER TABLE `student_courses`
  ADD PRIMARY KEY (`id`),
  ADD KEY `student_id` (`student_id`),
  ADD KEY `course_id` (`course_id`);

--
-- AUTO_INCREMENT for dumped tables
--

--
-- AUTO_INCREMENT for table `courses`
--
ALTER TABLE `courses`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=41;

--
-- AUTO_INCREMENT for table `lecturers`
--
ALTER TABLE `lecturers`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=14;

--
-- AUTO_INCREMENT for table `students`
--
ALTER TABLE `students`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=35;

--
-- AUTO_INCREMENT for table `student_courses`
--
ALTER TABLE `student_courses`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=33;
COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
