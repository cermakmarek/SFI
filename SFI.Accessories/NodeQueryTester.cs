﻿using IS4.SFI.Services;
using System;
using System.Collections.Generic;
using VDS.RDF;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;

namespace IS4.SFI
{
    /// <summary>
    /// Tests instances of <see cref="INode"/> whether they match
    /// any of the input SPARQL queries.
    /// </summary>
    public abstract class NodeQueryTester
    {
        /// <summary>
        /// The collection of queries to use when <see cref="Match(INode, out INodeMatchProperties)"/>
        /// is called.
        /// </summary>
        protected IReadOnlyCollection<SparqlQuery> Queries { get; }

        /// <summary>
        /// The instance of <see cref="IRdfHandler"/> that should be used for output triples.
        /// </summary>
        protected IRdfHandler Handler { get; }

        /// <summary>
        /// The SPARQL processor used to execute the queries.
        /// </summary>
        protected LeviathanQueryProcessor Processor { get; }

        /// <summary>
        /// Creates a new instance of the tester.
        /// </summary>
        /// <param name="rdfHandler">The RDF handler to use to store the result of CONSTRUCT queries.</param>
        /// <param name="queryGraph">The graph to process with the queries.</param>
        /// <param name="queries">The collection of SPARQL queries to process.</param>
        public NodeQueryTester(IRdfHandler rdfHandler, Graph queryGraph, IReadOnlyCollection<SparqlQuery> queries)
        {
            Queries = queries;
            Handler = rdfHandler;
            var dataset = new InMemoryDataset(queryGraph);
            Processor = new LeviathanQueryProcessor(dataset);
        }
        
        /// <summary>
        /// Matches an instance of <see cref="INode"/> against the stored SPARQL queries,
        /// and returns <see lang="true"/> if the node should be extracted.
        /// </summary>
        /// <param name="subject">The matching node to be identified by the queries.</param>
        /// <param name="properties">Additional variables from a successful match, as instances of <see cref="Uri"/> or <see cref="String"/>.</param>
        /// <returns><see langword="true"/> if the node should be extracted.</returns>
        public abstract bool Match(INode subject, out INodeMatchProperties properties);
    }
}
