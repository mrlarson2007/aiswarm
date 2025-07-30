from setuptools import setup, find_packages

setup(
    name="aiswarm",
    version="0.1.0",
    packages=find_packages(),
    include_package_data=True,
    install_requires=[
        'click',
        'requests',
    ],
    entry_points={
        'console_scripts': [
            'aiswarm = aiswarm_cli.main:cli',
        ],
    },
)
