SELECT 'CREATE DATABASE nextword_test'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'nextword_test')\gexec

SELECT 'CREATE DATABASE nextword_unit_test'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'nextword_unit_test')\gexec
