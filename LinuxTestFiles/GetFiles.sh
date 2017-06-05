# Create empty directories to hold the cached files
mkdir -p ~/ProcCopy1
mkdir -p ~/ProcCopy2

sudo rm -R ~/ProcCopy1/*
sudo rm -R ~/ProcCopy2/*

# Start the test program now (e.g. CPULoadTester)

# Obtain cpu and memory info files, plus stat files for all running processes
cd /proc
cp /proc/stat /proc/cpuinfo /proc/meminfo ~/ProcCopy1/

find . -maxdepth 1 -type d -exec mkdir ~/ProcCopy1/{} \; -exec cp "/proc/{}/stat" "/proc/{}/cmdline" ~/ProcCopy1/{}/ \;

# To retrieve stat files for every thread of the test program,
# use htop to determine the process id of the test program,
# then search for 34304 in the following statements and replace with the actual process ID
mkdir ~/ProcCopy1/34304/task
cd /proc/34304/task
find . -maxdepth 1 -type d -exec mkdir ~/ProcCopy1/34304/task/{} \; -exec cp "/proc/34304/task/{}/stat" "/proc/34304/task/{}/cmdline" ~/ProcCopy1/34304/task/{}/ \;

# After the test program has run for a while, obtain updated stat files
cd /proc
cp /proc/stat /proc/cpuinfo /proc/meminfo ~/ProcCopy2/

find . -maxdepth 1 -type d -exec mkdir ~/ProcCopy2/{} \; -exec cp "/proc/{}/stat" "/proc/{}/cmdline" ~/ProcCopy2/{}/ \;

mkdir ~/ProcCopy2/34304/task
cd /proc/34304/task
find . -maxdepth 1 -type d -exec mkdir ~/ProcCopy2/34304/task/{} \; -exec cp "/proc/34304/task/{}/stat" "/proc/34304/task/{}/cmdline" ~/ProcCopy2/34304/task/{}/ \;

# Compress the files for transfer
cd ~/ProcCopy1
tar -cf StatFiles1.tar *

cd ~/ProcCopy2
tar -cf StatFiles2.tar *
